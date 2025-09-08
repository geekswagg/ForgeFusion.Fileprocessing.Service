using Azure;
using Azure.Data.Tables;
using ForgeFusion.Fileprocessing.Service;

namespace ForgeFusion.Fileprocessing.Service.Models;

public class FileAuditEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "fileAudit"; // by default, can override per system needs
    public string RowKey { get; set; } = default!; // unique id (e.g., Guid)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string BlobName { get; set; } = default!;
    public string ContainerName { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string? Folder { get; set; }
    public FileProcessingStatus Status { get; set; }
    public FileActionType Action { get; set; }
    public string? ContentType { get; set; }
    public long ContentLength { get; set; }
    public string? Comment { get; set; }
    public string? CorrelationId { get; set; }
}
