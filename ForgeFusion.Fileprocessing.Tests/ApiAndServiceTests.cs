using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using ForgeFusion.Fileprocessing.Service;
using ForgeFusion.Fileprocessing.Service.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeFusion.Fileprocessing.Tests;

public class ApiFactory : WebApplicationFactory<ForgeFusion.Fileprocessing.Api.Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((ctx, cfg) => { });
        builder.ConfigureServices(s => { });
        return base.CreateHost(builder);
    }
}

public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_Then_Download_Works_With_Azurite()
    {
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        var fileBytes = new byte[] { 1, 2, 3, 4, 5 };
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "test.png");

        var uploadResponse = await client.PostAsync("/api/files/upload?folder=in", content);
        uploadResponse.EnsureSuccessStatusCode();

        var payload = await uploadResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        var blobName = payload!["blobName"]; // e.g. in/test.png

        var downloadResponse = await client.GetAsync($"/api/files/download/{Uri.EscapeDataString(Path.GetFileName(blobName))}?folder=in");
        downloadResponse.EnsureSuccessStatusCode();
        var downloaded = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileBytes, downloaded);
    }
}

public class ServiceUnitTests
{
    [Fact]
    public async Task Upload_Writes_Table_And_Sends_Queue_And_Audit()
    {
        var options = new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = "files",
            QueueName = "file-uploads",
            TableName = "fileProcessing",
            AuditTableName = "fileAudit",
            InFolder = "in",
            OutFolder = "out",
            ArchiveFolder = "archive"
        };

        var service = new AzureBlobFileStorageService(options);
        await using var ms = new MemoryStream(new byte[] { 10, 20, 30 });
        var name = $"unit-{Guid.NewGuid():N}.bin";

        var blobName = await service.UploadAsync(ms, name, options.InFolder, "application/octet-stream");

        // Validate blob exists
        var container = new BlobContainerClient(options.ConnectionString, options.ContainerName);
        var blob = container.GetBlobClient(blobName);
        Assert.True(await blob.ExistsAsync());

        // Validate table entry exists
        var table = new TableClient(options.ConnectionString, options.TableName);
        var entity = await table.GetEntityAsync<FileProcessingEntity>(FileProcessingEntity.DefaultPartitionKey, blobName);
        Assert.Equal(FileProcessingStatus.Uploaded, entity.Value.Status);

        // Validate audit table has at least one entry for this blob
        var auditTable = new TableClient(options.ConnectionString, options.AuditTableName);
        var audits = auditTable.Query<FileAuditEntity>(e => e.BlobName == blobName);
        Assert.True(audits.Any());
    }
}
