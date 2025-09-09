using ForgeFusion.Fileprocessing.Service;
using ForgeFusion.Fileprocessing.Service.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// Increase request size limits (adjust size as needed)
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // 200 MB
});
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 200 * 1024 * 1024; // 200 MB for multipart/form-data
});

// Bind options from configuration
var options = new BlobStorageOptions
{
    ConnectionString = builder.Configuration["Storage:ConnectionString"] ?? string.Empty,
    ContainerName = builder.Configuration["Storage:ContainerName"] ?? "files",
    QueueName = builder.Configuration["Storage:QueueName"] ?? "file-uploads",
    TableName = builder.Configuration["Storage:TableName"] ?? "fileProcessing",
    AuditTableName = builder.Configuration["Storage:AuditTableName"] ?? "fileAudit",
    InFolder = builder.Configuration["Storage:InFolder"] ?? "in",
    OutFolder = builder.Configuration["Storage:OutFolder"] ?? "out",
    ArchiveFolder = builder.Configuration["Storage:ArchiveFolder"] ?? "archive",
    AllowedContentTypes = builder.Configuration.GetSection("Storage:AllowedContentTypes").Get<string[]?>(),
    AllowedExtensions = builder.Configuration.GetSection("Storage:AllowedExtensions").Get<string[]?>()
};

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IFileStorageService, AzureBlobFileStorageService>();

// Minimal OpenAPI + Scalar UI
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "ForgeFusion Fileprocessing API",
            Version = "v1"
        };
        return Task.CompletedTask;
    });
});
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration["StorageConnection:blobServiceUri"]!).WithName("StorageConnection");
    clientBuilder.AddQueueServiceClient(builder.Configuration["StorageConnection:queueServiceUri"]!).WithName("StorageConnection");
    clientBuilder.AddTableServiceClient(builder.Configuration["StorageConnection:tableServiceUri"]!).WithName("StorageConnection");
});

var app = builder.Build();

// Expose OpenAPI document and Scalar UI
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "ForgeFusion Fileprocessing API";
    options.EndpointPathPrefix = string.Empty;
});

app.MapPost("/api/files/upload", async ([FromServices] IFileStorageService storage, HttpRequest request, CancellationToken ct, [FromQuery] string? fileName, [FromQuery] string? folder, [FromQuery] string? comment, [FromQuery] string? correlationId) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Content-Type must be multipart/form-data");

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.FirstOrDefault();
    if (file is null)
        return Results.BadRequest("No file provided");

    var effectiveName = string.IsNullOrWhiteSpace(fileName) ? file.FileName : fileName!;

    // Validate before upload
    var opts = app.Services.GetRequiredService<BlobStorageOptions>();
    FileValidation.Validate(effectiveName, file.ContentType, file.Length, opts);

    await using var stream = file.OpenReadStream();
    var blobName = await storage.UploadAsync(stream, effectiveName, folder, file.ContentType, correlationId, comment, ct);

    return Results.Ok(new { blobName });
})
// Per-endpoint limit (optional; keeps the rest of the app at defaults)
.WithMetadata(new RequestSizeLimitAttribute(200 * 1024 * 1024))
.Accepts<IFormFile>("multipart/form-data")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

// New: list files
app.MapGet("/api/files", async ([FromServices] IFileStorageService storage, [FromQuery] string? folder, CancellationToken ct) =>
{
    var items = new List<FileItem>();
    await foreach (var item in storage.ListFilesAsync(folder, ct))
        items.Add(item);
    return Results.Ok(items);
}).Produces<IEnumerable<FileItem>>(StatusCodes.Status200OK);

app.MapGet("/api/files/download/{blobName}", async ([FromServices] IFileStorageService storage, string blobName, HttpResponse response, [FromQuery] string? folder, CancellationToken ct) =>
{
    response.StatusCode = StatusCodes.Status200OK;
    await storage.DownloadAsync(blobName, response.Body, folder, ct);
}).Produces(StatusCodes.Status200OK);

app.MapPost("/api/files/archive/{blobName}", async ([FromServices] IFileStorageService storage, string blobName, [FromQuery] string? fromFolder, [FromQuery] string? correlationId, [FromQuery] string? comment, CancellationToken ct) =>
{
    var archived = await storage.ArchiveAsync(blobName, fromFolder, correlationId, comment, ct);
    return Results.Ok(new { archived });
}).Produces(StatusCodes.Status200OK);

// New: return counts by file type (extension) optionally scoped to folder
app.MapGet("/api/files/types", async ([FromServices] IFileStorageService storage, [FromQuery] string? folder, CancellationToken ct) =>
{
    var counts = await storage.GetFileTypeCountsAsync(folder, ct);
    return Results.Ok(counts);
}).Produces<IEnumerable<FileTypeCount>>(StatusCodes.Status200OK);

// New: audit/history endpoint
app.MapGet("/api/files/audit", async ([FromServices] IFileStorageService storage, [FromQuery] string? blobName, [FromQuery] string? folder, [FromQuery] int? take, CancellationToken ct) =>
{
    var audits = await storage.GetAuditAsync(blobName, folder, take, ct);
    return Results.Ok(audits);
}).Produces<IEnumerable<FileAuditEntity>>(StatusCodes.Status200OK);

app.Run();
