using ForgeFusion.Fileprocessing.Web.Models;
using System.Net.Http.Json;

namespace ForgeFusion.Fileprocessing.Web.Services;

public interface IFileProcessingApiService
{
    Task<List<FileItem>> GetFilesAsync(string? folder = null, CancellationToken cancellationToken = default);
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string? folder = null, string? comment = null, string? correlationId = null, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(string fileName, string? folder, Stream destinationStream, CancellationToken cancellationToken = default);
    Task<bool> ArchiveFileAsync(string fileName, string? folder = null, string? correlationId = null, string? comment = null, CancellationToken cancellationToken = default);
    Task<List<FileTypeCount>> GetFileTypeCountsAsync(string? folder = null, CancellationToken cancellationToken = default);
    Task<List<FileAuditEntity>> GetAuditHistoryAsync(string? blobName = null, string? folder = null, int? take = null, CancellationToken cancellationToken = default);
}

public class FileProcessingApiService : IFileProcessingApiService
{
    private readonly HttpClient _httpClient;

    public FileProcessingApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("FileProcessingApi");
    }

    public async Task<List<FileItem>> GetFilesAsync(string? folder = null, CancellationToken cancellationToken = default)
    {
        var url = "/api/files";
        if (!string.IsNullOrEmpty(folder))
            url += $"?folder={Uri.EscapeDataString(folder)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<FileItem>>(cancellationToken) ?? new List<FileItem>();
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string? folder = null, string? comment = null, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "file", fileName);

        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(folder))
            queryParams.Add($"folder={Uri.EscapeDataString(folder)}");
        if (!string.IsNullOrEmpty(comment))
            queryParams.Add($"comment={Uri.EscapeDataString(comment)}");
        if (!string.IsNullOrEmpty(correlationId))
            queryParams.Add($"correlationId={Uri.EscapeDataString(correlationId)}");

        var url = "/api/files/upload";
        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken);
        return result?["blobName"] ?? fileName;
    }

    public async Task DownloadFileAsync(string fileName, string? folder, Stream destinationStream, CancellationToken cancellationToken = default)
    {
        var url = $"/api/files/download/{Uri.EscapeDataString(fileName)}";
        if (!string.IsNullOrEmpty(folder))
            url += $"?folder={Uri.EscapeDataString(folder)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await response.Content.CopyToAsync(destinationStream, cancellationToken);
    }

    public async Task<bool> ArchiveFileAsync(string fileName, string? folder = null, string? correlationId = null, string? comment = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(folder))
            queryParams.Add($"fromFolder={Uri.EscapeDataString(folder)}");
        if (!string.IsNullOrEmpty(correlationId))
            queryParams.Add($"correlationId={Uri.EscapeDataString(correlationId)}");
        if (!string.IsNullOrEmpty(comment))
            queryParams.Add($"comment={Uri.EscapeDataString(comment)}");

        var url = $"/api/files/archive/{Uri.EscapeDataString(fileName)}";
        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, bool>>(cancellationToken);
        return result?["archived"] ?? false;
    }

    public async Task<List<FileTypeCount>> GetFileTypeCountsAsync(string? folder = null, CancellationToken cancellationToken = default)
    {
        var url = "/api/files/types";
        if (!string.IsNullOrEmpty(folder))
            url += $"?folder={Uri.EscapeDataString(folder)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<FileTypeCount>>(cancellationToken) ?? new List<FileTypeCount>();
    }

    public async Task<List<FileAuditEntity>> GetAuditHistoryAsync(string? blobName = null, string? folder = null, int? take = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(blobName))
            queryParams.Add($"blobName={Uri.EscapeDataString(blobName)}");
        if (!string.IsNullOrEmpty(folder))
            queryParams.Add($"folder={Uri.EscapeDataString(folder)}");
        if (take.HasValue)
            queryParams.Add($"take={take.Value}");

        var url = "/api/files/audit";
        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<FileAuditEntity>>(cancellationToken) ?? new List<FileAuditEntity>();
    }
}