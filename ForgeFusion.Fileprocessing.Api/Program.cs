using ForgeFusion.Fileprocessing.Service;
using ForgeFusion.Fileprocessing.Service.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using ForgeFusion.Fileprocessing.Api.Services;
using System.ComponentModel.DataAnnotations;

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
    AllowedExtensions = builder.Configuration.GetSection("Storage:AllowedExtensions").Get<string[]?>(),
    MaxFileSize = builder.Configuration.GetValue<long?>("Storage:MaxFileSize")
};

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IFileStorageService, AzureBlobFileStorageService>();
builder.Services.AddSingleton<IConsoleLogger, SpectreConsoleLogger>();

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

// Print enhanced startup banner
var consoleLogger = app.Services.GetRequiredService<IConsoleLogger>();
consoleLogger.WriteStartupBanner(app.Environment.EnvironmentName, options.ContainerName, options.MaxFileSize);

// Handle graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    SpectreConsoleExtensions.WriteShutdownMessage();
});

// Expose OpenAPI document and Scalar UI
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "ForgeFusion Fileprocessing API";
    options.EndpointPathPrefix = string.Empty;
});

app.MapPost("/api/files/upload", async ([FromServices] IFileStorageService storage, [FromServices] IConsoleLogger logger, HttpRequest request, CancellationToken ct, [FromQuery] string? fileName, [FromQuery] string? folder, [FromQuery] string? comment, [FromQuery] string? correlationId) =>
{
    try
    {
        if (!request.HasFormContentType)
        {
            logger.LogError("Upload", "Content-Type must be multipart/form-data", correlationId);
            return Results.BadRequest("Content-Type must be multipart/form-data");
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file is null)
        {
            logger.LogError("Upload", "No file provided", correlationId);
            return Results.BadRequest("No file provided");
        }

        var effectiveName = string.IsNullOrWhiteSpace(fileName) ? file.FileName : fileName!;

        // Validate before upload
        var opts = app.Services.GetRequiredService<BlobStorageOptions>();
        try
        {
            FileValidation.Validate(effectiveName, file.ContentType, file.Length, opts);
        }
        catch (ValidationException ex)
        {
            logger.LogValidation(effectiveName, ex.Message, correlationId);
            return Results.BadRequest(ex.Message);
        }

        await using var stream = file.OpenReadStream();
        var blobName = await storage.UploadAsync(stream, effectiveName, folder, file.ContentType, correlationId, comment, ct);

        logger.LogUpload(effectiveName, folder ?? opts.InFolder, file.Length, correlationId);
        return Results.Ok(new { blobName });
    }
    catch (Exception ex)
    {
        logger.LogError("Upload", ex.Message, correlationId);
        throw;
    }
})
// Per-endpoint limit (optional; keeps the rest of the app at defaults)
.WithMetadata(new RequestSizeLimitAttribute(200 * 1024 * 1024))
.Accepts<IFormFile>("multipart/form-data")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

// New: list files
app.MapGet("/api/files", async ([FromServices] IFileStorageService storage, [FromServices] IConsoleLogger logger, [FromQuery] string? folder, CancellationToken ct) =>
{
    try
    {
        var items = new List<FileItem>();
        await foreach (var item in storage.ListFilesAsync(folder, ct))
            items.Add(item);

        logger.LogListFiles(folder, items.Count);
        return Results.Ok(items);
    }
    catch (Exception ex)
    {
        logger.LogError("List Files", ex.Message);
        throw;
    }
}).Produces<IEnumerable<FileItem>>(StatusCodes.Status200OK);

app.MapGet("/api/files/download/{blobName}", async ([FromServices] IFileStorageService storage, [FromServices] IConsoleLogger logger, string blobName, HttpResponse response, [FromQuery] string? folder, CancellationToken ct) =>
{
    try
    {
        response.StatusCode = StatusCodes.Status200OK;
        await storage.DownloadAsync(blobName, response.Body, folder, ct);
        logger.LogDownload(blobName, folder, null);
    }
    catch (Exception ex)
    {
        logger.LogError("Download", ex.Message);
        throw;
    }
}).Produces(StatusCodes.Status200OK);

app.MapPost("/api/files/archive/{blobName}", async ([FromServices] IFileStorageService storage, [FromServices] IConsoleLogger logger, string blobName, [FromQuery] string? fromFolder, [FromQuery] string? correlationId, [FromQuery] string? comment, CancellationToken ct) =>
{
    try
    {
        var archived = await storage.ArchiveAsync(blobName, fromFolder, correlationId, comment, ct);
        logger.LogArchive(blobName, fromFolder, correlationId);
        return Results.Ok(new { archived });
    }
    catch (Exception ex)
    {
        logger.LogError("Archive", ex.Message, correlationId);
        throw;
    }
}).Produces(StatusCodes.Status200OK);

// New: return counts by file type (extension) optionally scoped to folder
app.MapGet("/api/files/types", async ([FromServices] IFileStorageService storage, [FromServices] IConsoleLogger logger, [FromQuery] string? folder, CancellationToken ct) =>
{
    try
    {
        var counts = await storage.GetFileTypeCountsAsync(folder, ct);
        logger.LogGetFileTypes(folder, counts.Count);
        return Results.Ok(counts);
    }
    catch (Exception ex)
    {
        logger.LogError("Get File Types", ex.Message);
        throw;
    }
}).Produces<IEnumerable<FileTypeCount>>(StatusCodes.Status200OK);

// New: audit/history endpoint
app.MapGet("/api/files/audit", async ([FromServices] IFileStorageService storage, [FromServices] IConsoleLogger logger, [FromQuery] string? blobName, [FromQuery] string? folder, [FromQuery] int? take, CancellationToken ct) =>
{
    try
    {
        var audits = await storage.GetAuditAsync(blobName, folder, take, ct);
        logger.LogGetAudit(blobName, folder, take, audits.Count);
        return Results.Ok(audits);
    }
    catch (Exception ex)
    {
        logger.LogError("Get Audit", ex.Message);
        throw;
    }
}).Produces<IEnumerable<FileAuditEntity>>(StatusCodes.Status200OK);

app.Run();
