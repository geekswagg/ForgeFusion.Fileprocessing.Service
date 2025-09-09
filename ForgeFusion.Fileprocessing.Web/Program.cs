using ForgeFusion.Fileprocessing.Web.Components;
using ForgeFusion.Fileprocessing.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add HttpClient for API calls
builder.Services.AddHttpClient("FileProcessingApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FileProcessingApi:BaseUrl"] ?? "https://localhost:5001");
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
