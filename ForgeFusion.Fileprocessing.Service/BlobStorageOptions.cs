namespace ForgeFusion.Fileprocessing.Service;

public class BlobStorageOptions
{
    public required string ConnectionString { get; set; }
    public required string ContainerName { get; set; }
    public required string QueueName { get; set; }
    public required string TableName { get; set; }

    // Optional audit/history table (defaults to "fileAudit")
    public string AuditTableName { get; set; } = "fileAudit";

    // Folder names, defaults provided
    public string InFolder { get; set; } = "in";
    public string OutFolder { get; set; } = "out";
    public string ArchiveFolder { get; set; } = "archive";

    // Optional file validation configuration
    public string[]? AllowedContentTypes { get; set; }
    public string[]? AllowedExtensions { get; set; }
}
