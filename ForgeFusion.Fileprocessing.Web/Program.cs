using ForgeFusion.Fileprocessing.Web.Components;
using ForgeFusion.Fileprocessing.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR for better file upload handling
builder.Services.AddSignalR(options =>
{
    // Increase buffer size for large file uploads
    options.MaximumReceiveMessageSize = 200 * 1024 * 1024; // 200MB
    options.StreamBufferCapacity = 10;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    
    // Increase timeouts for long operations
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// Configure circuit options for better connection handling
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    // Increase timeouts for file upload operations
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
    options.DisconnectedCircuitMaxRetained = 100;
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
    
    if (builder.Environment.IsDevelopment())
    {
        options.DetailedErrors = true;
    }
});

// Add HttpClient for API calls
builder.Services.AddHttpClient("FileProcessingApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FileProcessingApi:BaseUrl"] ?? "https://localhost:7200");
    client.Timeout = TimeSpan.FromMinutes(10); // For large file uploads
});

// Register our API service
builder.Services.AddScoped<IFileProcessingApiService, FileProcessingApiService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
