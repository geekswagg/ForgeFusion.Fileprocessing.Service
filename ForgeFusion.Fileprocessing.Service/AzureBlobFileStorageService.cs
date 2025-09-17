using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using System.Text.Json;
using ForgeFusion.Fileprocessing.Service.Models;

namespace ForgeFusion.Fileprocessing.Service;

public interface IFileStorageService
{
    Task<string> UploadAsync(Stream content, string fileName, string? folder = null, string? contentType = null, string? correlationId = null, string? comment = null, CancellationToken cancellationToken = default);
    Task DownloadAsync(string blobName, Stream destination, string? folder = null, CancellationToken cancellationToken = default);
    Task<string> ArchiveAsync(string blobName, string? fromFolder = null, string? correlationId = null, string? comment = null, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(string blobName, FileProcessingStatus status, string? folder = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileTypeCount>> GetFileTypeCountsAsync(string? folder = null, CancellationToken cancellationToken = default);

    // New list and audit APIs
    IAsyncEnumerable<ForgeFusion.Fileprocessing.Service.Models.FileItem> ListFilesAsync(string? folder = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileAuditEntity>> GetAuditAsync(string? blobName = null, string? folder = null, int? take = null, CancellationToken cancellationToken = default);
}

public class AzureBlobFileStorageService : IFileStorageService
{
    private readonly BlobContainerClient _container;
    private readonly QueueClient _queue;
    private readonly TableClient _table;
    private readonly TableClient _auditTable;
    private readonly BlobStorageOptions _options;

    public AzureBlobFileStorageService(BlobStorageOptions options)
    {
        _options = options;
        _container = new BlobContainerClient(options.ConnectionString, options.ContainerName);
        _queue = new QueueClient(options.ConnectionString, options.QueueName);
        _table = new TableClient(options.ConnectionString, options.TableName);
        _auditTable = new TableClient(options.ConnectionString, options.AuditTableName);
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string? folder = null, string? contentType = null, string? correlationId = null, string? comment = null, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken).ConfigureAwait(false);

        folder ??= _options.InFolder;
        var blobName = Combine(folder, fileName);
        var blob = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders();
        if (!string.IsNullOrEmpty(contentType))
            headers.ContentType = contentType;

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = headers
        }, cancellationToken).ConfigureAwait(false);

        // table entry
        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var entity = new FileProcessingEntity
        {
            RowKey = ToRowKey(blobName),
            FileName = fileName,
            ContainerName = _options.ContainerName,
            Folder = folder,
            Status = FileProcessingStatus.Uploaded,
            CorrelationId = correlationId,
            ContentType = properties.Value.ContentType,
            ContentLength = properties.Value.ContentLength
        };
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

        // queue event
        var evt = new FileUploadedEvent(blobName, _options.ContainerName, folder, correlationId, entity.ContentType, entity.ContentLength, DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(evt);
        await _queue.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)), cancellationToken).ConfigureAwait(false);

        // audit entry
        await LogAuditAsync(new FileAuditEntity
        {
            RowKey = Guid.NewGuid().ToString("N"),
            BlobName = blobName,
            ContainerName = _options.ContainerName,
            FileName = fileName,
            Folder = folder,
            Status = FileProcessingStatus.Uploaded,
            Action = FileActionType.Upload,
            ContentType = entity.ContentType,
            ContentLength = entity.ContentLength,
            Comment = comment,
            CorrelationId = correlationId
        }, cancellationToken).ConfigureAwait(false);

        return blobName;
    }

    public async Task DownloadAsync(string blobName, Stream destination, string? folder = null, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken).ConfigureAwait(false);
        var path = folder is null ? blobName : Combine(folder, blobName);
        var blob = _container.GetBlobClient(path);
        var props = await blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await blob.DownloadToAsync(destination, cancellationToken).ConfigureAwait(false);

        await LogAuditAsync(new FileAuditEntity
        {
            RowKey = Guid.NewGuid().ToString("N"),
            BlobName = path,
            ContainerName = _options.ContainerName,
            FileName = Path.GetFileName(path),
            Folder = folder,
            Status = FileProcessingStatus.Processed, // download does not change processing status, but we log action
            Action = FileActionType.Download,
            ContentType = props.Value.ContentType,
            ContentLength = props.Value.ContentLength
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ArchiveAsync(string blobName, string? fromFolder = null, string? correlationId = null, string? comment = null, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken).ConfigureAwait(false);
        var sourcePath = fromFolder is null ? blobName : Combine(fromFolder, blobName);
        var sourceBlob = _container.GetBlobClient(sourcePath);
        if (!await sourceBlob.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new RequestFailedException(404, $"Blob not found: {sourcePath}");
        }

        var destPath = Combine(_options.ArchiveFolder, Path.GetFileName(sourcePath));
        var destBlob = _container.GetBlobClient(destPath);

        // Copy and then delete source
        var copyResponse = await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
        // Poll copy status (simple loop with small delay)
        BlobProperties destProps;
        do
        {
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            destProps = (await destBlob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Value;
        } while (destProps.CopyStatus == CopyStatus.Pending);

        if (destProps.CopyStatus != CopyStatus.Success)
        {
            throw new RequestFailedException(500, $"Copy to archive failed with status {destProps.CopyStatus}");
        }

        await sourceBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken).ConfigureAwait(false);

        // update status record
        await UpdateStatusAsync(Path.GetFileName(sourcePath), FileProcessingStatus.Archived, _options.ArchiveFolder, cancellationToken).ConfigureAwait(false);

        // audit
        await LogAuditAsync(new FileAuditEntity
        {
            RowKey = Guid.NewGuid().ToString("N"),
            BlobName = destPath,
            ContainerName = _options.ContainerName,
            FileName = Path.GetFileName(destPath),
            Folder = _options.ArchiveFolder,
            Status = FileProcessingStatus.Archived,
            Action = FileActionType.Archive,
            ContentType = destProps.ContentType,
            ContentLength = destProps.ContentLength,
            CorrelationId = correlationId,
            Comment = comment
        }, cancellationToken).ConfigureAwait(false);

        return destPath;
    }

    public async Task UpdateStatusAsync(string blobName, FileProcessingStatus status, string? folder = null, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken).ConfigureAwait(false);
        var path = folder is null ? blobName : Combine(folder, blobName);
        var key = ToRowKey(path);

        try
        {
            var response = await _table.GetEntityAsync<FileProcessingEntity>(FileProcessingEntity.DefaultPartitionKey, key, cancellationToken: cancellationToken).ConfigureAwait(false);
            var entity = response.Value;
            entity.Status = status;
            entity.Folder = InferFolderFromStatus(status);
            await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // create if not exist
            var entity = new FileProcessingEntity
            {
                RowKey = key,
                FileName = Path.GetFileName(path),
                ContainerName = _options.ContainerName,
                Folder = InferFolderFromStatus(status),
                Status = status
            };
            await _table.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<FileTypeCount>> GetFileTypeCountsAsync(string? folder = null, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken).ConfigureAwait(false);

        var results = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        await foreach (var page in _container.GetBlobsAsync(prefix: string.IsNullOrWhiteSpace(folder) ? null : folder.TrimEnd('/') + "/").AsPages())
        {
            foreach (var item in page.Values)
            {
                // get extension from name (after last '.')
                var name = item.Name;
                var ext = Path.GetExtension(name);
                var key = string.IsNullOrWhiteSpace(ext) ? "(none)" : ext.TrimStart('.');
                results[key] = results.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }

        return results.Select(kvp => new FileTypeCount { FileType = kvp.Key, Count = kvp.Value }).OrderByDescending(x => x.Count).ThenBy(x => x.FileType, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async IAsyncEnumerable<FileItem> ListFilesAsync(string? folder = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken).ConfigureAwait(false);
        var prefix = string.IsNullOrWhiteSpace(folder) ? null : folder!.TrimEnd('/') + "/";
        var pages = _container.GetBlobsAsync(prefix: prefix).AsPages();
        await foreach (var page in pages.WithCancellation(cancellationToken))
        {
            foreach (var item in page.Values)
            {
                yield return new FileItem
                {
                    Name = Path.GetFileName(item.Name),
                    Folder = GetFolderFromName(item.Name),
                    ContentLength = item.Properties.ContentLength ?? 0,
                    ContentType = item.Properties.ContentType ?? "",
                    LastModified = item.Properties.LastModified ?? DateTimeOffset.MinValue
                };
            }
        }
    }

    public async Task<IReadOnlyList<FileAuditEntity>> GetAuditAsync(string? blobName = null, string? folder = null, int? take = null, CancellationToken cancellationToken = default)
    {
        await EnsureResourcesAsync(cancellationToken).ConfigureAwait(false);

        // Build query using SDK filter support
        var filter = new List<string>();
        if (!string.IsNullOrWhiteSpace(blobName))
        {
            // exact match on stored blob path (may include folder prefix). If only name provided and folder provided, combine
            var name = string.IsNullOrWhiteSpace(folder) ? blobName! : Combine(folder!, blobName!);
            filter.Add($"BlobName eq '{name.Replace("'", "''")}'");
        }
        if (!string.IsNullOrWhiteSpace(folder) && string.IsNullOrWhiteSpace(blobName))
        {
            // filter by prefix is not supported directly in OData for Table, so retrieve and filter client-side
        }

        var results = new List<FileAuditEntity>();
        if (filter.Count > 0)
        {
            await foreach (var entity in _auditTable.QueryAsync<FileAuditEntity>(filter: string.Join(" and ", filter), cancellationToken: cancellationToken))
            {
                results.Add(entity);
                if (take is int t && results.Count >= t) break;
            }
        }
        else
        {
            await foreach (var entity in _auditTable.QueryAsync<FileAuditEntity>(e => e.PartitionKey == "fileAudit", cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    if (!string.Equals(entity.Folder, folder, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                results.Add(entity);
                if (take is int t && results.Count >= t) break;
            }
        }

        // Order by timestamp desc
        return results.OrderByDescending(e => e.Timestamp).ToList();
    }

    private static string Combine(string folder, string fileName)
        => string.IsNullOrWhiteSpace(folder) ? fileName : folder.TrimEnd('/') + "/" + fileName;

    private static string GetFolderFromName(string fullName)
    {
        var idx = fullName.IndexOf('/');
        return idx > 0 ? fullName.Substring(0, idx) : string.Empty;
    }

    private string InferFolderFromStatus(FileProcessingStatus status) => status switch
    {
        FileProcessingStatus.Initial or FileProcessingStatus.Uploaded or FileProcessingStatus.Processing => _options.InFolder,
        FileProcessingStatus.Processed => _options.OutFolder,
        FileProcessingStatus.Archived => _options.ArchiveFolder,
        _ => _options.InFolder
    };

    private async Task EnsureResourcesAsync(CancellationToken ct)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct).ConfigureAwait(false);
        await _queue.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
        await _table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        await _auditTable.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
    }

    private async Task LogAuditAsync(FileAuditEntity entry, CancellationToken ct)
    {
        // Enforce partition key to configured audit table prefix
        entry.PartitionKey = "fileAudit";
        await _auditTable.AddEntityAsync(entry, ct).ConfigureAwait(false);
    }

    // RowKey must not contain '/', '\\', '#', '?' or control characters
    private static string ToRowKey(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        Span<char> buffer = stackalloc char[input.Length];
        int idx = 0;
        foreach (var ch in input)
        {
            buffer[idx++] = ch switch
            {
                '/' or '\\' or '#' or '?' => ':',
                >= '\u0000' and <= '\u001F' => '_',
                '\u007F' => '_',
                _ => ch
            };
        }
        return new string(buffer.Slice(0, idx));
    }
}
