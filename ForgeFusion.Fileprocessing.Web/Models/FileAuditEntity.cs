namespace ForgeFusion.Fileprocessing.Web.Models;

public class FileAuditEntity
{
    public string PartitionKey { get; set; } = "fileAudit";
    public string RowKey { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
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

public enum FileProcessingStatus
{
    Initial = 0,
    Uploaded = 1,
    Processing = 2,
    Processed = 3,
    Archived = 4,
}

public enum FileActionType
{
    Upload = 0,
    Download = 1,
    Archive = 2,
    Delete = 3,
}