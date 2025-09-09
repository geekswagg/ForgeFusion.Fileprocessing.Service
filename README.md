# ForgeFusion File Processing Service

[![Build Status](https://dev.azure.com/your-org/ForgeFusion/_apis/build/status/ForgeFusion.Fileprocessing?branchName=main)](https://dev.azure.com/your-org/ForgeFusion/_build/latest?definitionId=1&branchName=main)
[![GitHub Actions](https://github.com/your-org/ForgeFusion.Fileprocessing/workflows/CI/badge.svg)](https://github.com/your-org/ForgeFusion.Fileprocessing/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A comprehensive file processing service built with .NET 9, Azure Storage, and Blazor Server. This solution provides secure file upload, download, archival, and audit capabilities with a modern web interface.

## ?? Features

### Core Functionality
- **File Upload**: Secure multi-file upload with validation and progress tracking
- **File Download**: Direct download with proper content-type handling  
- **File Archival**: Move files between folders with audit logging
- **File Browsing**: Filter, search, and sort files by various criteria
- **Audit Trail**: Complete history of all file operations with correlation IDs

### Technical Features
- **Azure Storage Integration**: Blob storage, Tables, and Queues
- **Real-time UI**: Blazor Server with SignalR for responsive interactions
- **File Validation**: Content-type and extension validation with size limits
- **Enhanced Logging**: Beautiful console output with Spectre.Console
- **API Documentation**: Interactive Scalar UI documentation
- **High Performance**: Support for files up to 200MB with optimized upload handling

## ??? Architecture

The solution consists of four main projects:

```
ForgeFusion.Fileprocessing.Service/
??? ForgeFusion.Fileprocessing.Api/          # REST API with minimal APIs
??? ForgeFusion.Fileprocessing.Web/          # Blazor Server web application  
??? ForgeFusion.Fileprocessing.Service/      # Core business logic and Azure services
??? ForgeFusion.Fileprocessing.Tests/        # Integration and unit tests
```

### Project Dependencies
- **API** ? Service (core logic)
- **Web** ? Service (via HTTP API calls)
- **Tests** ? API + Service (integration testing)

## ??? Technology Stack

- **.NET 9**: Latest framework with C# 13 features
- **Azure Storage**: Blob storage, Tables, and Queues
- **Blazor Server**: Real-time web UI with SignalR
- **Minimal APIs**: Lightweight API endpoints
- **Scalar UI**: Interactive API documentation
- **Spectre.Console**: Enhanced console logging
- **xUnit**: Testing framework

## ?? Prerequisites

### Development Environment
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Storage Emulator (Azurite)](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) or [VS Code](https://code.visualstudio.com/)

### Azure Resources (Production)
- Azure Storage Account
- Azure App Service (2 instances for API and Web)
- Azure Application Insights (optional)

## ?? Quick Start

### 1. Clone the Repository
```bash
git clone https://github.com/your-org/ForgeFusion.Fileprocessing.git
cd ForgeFusion.Fileprocessing
```

### 2. Start Azure Storage Emulator
```bash
# Install Azurite globally
npm install -g azurite

# Start Azurite
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

### 3. Run the Applications

#### Terminal 1 - API
```bash
cd ForgeFusion.Fileprocessing.Api
dotnet run
# API will be available at https://localhost:7200
```

#### Terminal 2 - Web Application  
```bash
cd ForgeFusion.Fileprocessing.Web
dotnet run
# Web app will be available at https://localhost:7099
```

### 4. Open in Browser
- **Web Application**: https://localhost:7099
- **API Documentation**: https://localhost:7200

## ?? Configuration

### Development (appsettings.json)

#### API Configuration
```json
{
  "Storage": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "ContainerName": "files",
    "QueueName": "file-uploads", 
    "TableName": "fileProcessing",
    "AuditTableName": "fileAudit",
    "InFolder": "in",
    "OutFolder": "out", 
    "ArchiveFolder": "archive",
    "MaxFileSize": 209715200,
    "AllowedContentTypes": [
      "application/pdf",
      "image/png",
      "image/jpeg", 
      "text/csv",
      "application/json"
    ],
    "AllowedExtensions": [
      ".pdf",
      ".png", 
      ".jpg",
      ".jpeg",
      ".csv",
      ".json"
    ]
  }
}
```

#### Web Configuration
```json
{
  "FileProcessingApi": {
    "BaseUrl": "https://localhost:7200"
  }
}
```

### Production Configuration

Use Azure App Configuration or Environment Variables:

```bash
Storage__ConnectionString=DefaultEndpointsProtocol=https;AccountName=...
FileProcessingApi__BaseUrl=https://your-api.azurewebsites.net
```

## ?? API Endpoints

### File Operations
- `POST /api/files/upload` - Upload files with metadata
- `GET /api/files` - List files with optional folder filtering
- `GET /api/files/download/{blobName}` - Download specific file
- `POST /api/files/archive/{blobName}` - Archive file to archive folder

### Analytics & Audit
- `GET /api/files/types` - Get file type statistics  
- `GET /api/files/audit` - Get audit history with filtering

### Example Usage

#### Upload File
```bash
curl -X POST "https://localhost:7200/api/files/upload?folder=in&comment=Test%20upload" \
  -F "file=@document.pdf"
```

#### List Files
```bash
curl "https://localhost:7200/api/files?folder=in"
```

## ?? Testing

### Run Unit Tests
```bash
dotnet test ForgeFusion.Fileprocessing.Tests
```

### Run Integration Tests (requires Azurite)
```bash
# Start Azurite first
azurite

# Run tests
dotnet test --filter "Category=Integration"
```

## ?? Deployment

### Azure App Service Deployment

1. **Create Azure Resources**
   ```bash
   # Create resource group
   az group create --name rg-forgefusion --location eastus
   
   # Create storage account
   az storage account create --name stforgefusion --resource-group rg-forgefusion --sku Standard_LRS
   
   # Create app service plans
   az appservice plan create --name asp-forgefusion --resource-group rg-forgefusion --sku B1
   
   # Create web apps
   az webapp create --name forgefusion-api --resource-group rg-forgefusion --plan asp-forgefusion
   az webapp create --name forgefusion-web --resource-group rg-forgefusion --plan asp-forgefusion
   ```

2. **Configure Application Settings**
   ```bash
   # Set storage connection string
   az webapp config appsettings set --name forgefusion-api --resource-group rg-forgefusion \
     --settings Storage__ConnectionString="DefaultEndpointsProtocol=https;AccountName=stforgefusion;..."
   
   # Set API base URL for web app
   az webapp config appsettings set --name forgefusion-web --resource-group rg-forgefusion \
     --settings FileProcessingApi__BaseUrl="https://forgefusion-api.azurewebsites.net"
   ```

### Container Deployment

```dockerfile
# Dockerfile.api
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ForgeFusion.Fileprocessing.Api/ForgeFusion.Fileprocessing.Api.csproj", "ForgeFusion.Fileprocessing.Api/"]
COPY ["ForgeFusion.Fileprocessing.Service/ForgeFusion.Fileprocessing.Service.csproj", "ForgeFusion.Fileprocessing.Service/"]
RUN dotnet restore "ForgeFusion.Fileprocessing.Api/ForgeFusion.Fileprocessing.Api.csproj"
COPY . .
WORKDIR "/src/ForgeFusion.Fileprocessing.Api"
RUN dotnet build "ForgeFusion.Fileprocessing.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ForgeFusion.Fileprocessing.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ForgeFusion.Fileprocessing.Api.dll"]
```

## ?? Development

### Code Structure
```
??? ForgeFusion.Fileprocessing.Api/
?   ??? Program.cs                    # API startup and endpoint definitions
?   ??? Services/                     # Console logging services
?   ??? appsettings.json             # API configuration
??? ForgeFusion.Fileprocessing.Web/
?   ??? Components/Pages/            # Blazor pages (Upload, Files, etc.)
?   ??? Services/                    # HTTP API client services  
?   ??? wwwroot/                     # Static assets and JavaScript
?   ??? Program.cs                   # Web app startup
??? ForgeFusion.Fileprocessing.Service/
?   ??? Models/                      # Data models and DTOs
?   ??? AzureBlobFileStorageService.cs # Core storage implementation
?   ??? FileValidation.cs           # File validation logic
?   ??? BlobStorageOptions.cs       # Configuration options
??? ForgeFusion.Fileprocessing.Tests/
    ??? ApiAndServiceTests.cs        # Integration tests
```

### Adding New Features

1. **Add new API endpoint** in `Api/Program.cs`
2. **Create corresponding service method** in `Service/`
3. **Add web UI** in `Web/Components/Pages/`
4. **Write tests** in `Tests/`

### File Processing Workflow

1. File uploaded to `/api/files/upload`
2. Validation performed (size, type, extension)
3. File stored in Azure Blob Storage
4. Metadata recorded in Azure Tables
5. Queue message sent for further processing
6. Audit entry created

## ?? Security

### File Validation
- Content-type verification
- File extension allowlist
- Maximum file size limits (200MB default)
- Malware scanning integration points

### Authentication (Future)
- Azure AD B2C integration ready
- API key authentication support
- Role-based access control

## ?? Monitoring

### Built-in Logging
- Structured logging with correlation IDs
- Beautiful console output with Spectre.Console
- Request/response logging for all operations

### Azure Monitoring (Production)
- Application Insights integration
- Custom metrics and telemetry
- Performance monitoring
- Error tracking and alerting

## ?? Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`) 
5. Open a Pull Request

### Code Standards
- Follow .NET coding conventions
- Write unit tests for new features
- Update documentation as needed
- Ensure all tests pass before submitting PR

## ?? License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## ?? Acknowledgments

- [Spectre.Console](https://github.com/spectreconsole/spectre.console) for beautiful console output
- [Scalar](https://github.com/scalar/scalar) for API documentation
- [Azure SDK for .NET](https://github.com/Azure/azure-sdk-for-net) for Azure integration

## ?? Support

- Create an [issue](https://github.com/your-org/ForgeFusion.Fileprocessing/issues) for bug reports
- Start a [discussion](https://github.com/your-org/ForgeFusion.Fileprocessing/discussions) for questions
- Check the [wiki](https://github.com/your-org/ForgeFusion.Fileprocessing/wiki) for detailed guides

---

**Built with ?? using .NET 9 and Azure**