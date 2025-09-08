using ForgeFusion.Fileprocessing.Service;
using ForgeFusion.Fileprocessing.Service.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
.Accepts<IFormFile>("multipart/form-data")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

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

app.Run();
