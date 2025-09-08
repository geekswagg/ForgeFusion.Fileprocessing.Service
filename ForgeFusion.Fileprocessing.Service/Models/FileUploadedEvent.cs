namespace ForgeFusion.Fileprocessing.Service.Models;

public record FileUploadedEvent(
    string BlobName,
    string ContainerName,
    string? Folder,
    string? CorrelationId,
    string? ContentType,
    long ContentLength,
    DateTimeOffset UploadedAtUtc
);
