using Azure;
using Azure.Data.Tables;

namespace ForgeFusion.Fileprocessing.Service.Models;

public class FileProcessingEntity : ITableEntity
{
    public const string DefaultPartitionKey = "fileProcessing";

    public string PartitionKey { get; set; } = DefaultPartitionKey;
    public string RowKey { get; set; } = default!; // file id or blob name
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string FileName { get; set; } = default!; // original file name
    public string ContainerName { get; set; } = default!;
    public string Folder { get; set; } = default!; // in, out, archive
    public FileProcessingStatus Status { get; set; }
    public string? CorrelationId { get; set; }
    public string? ContentType { get; set; }
    public long ContentLength { get; set; }
}
