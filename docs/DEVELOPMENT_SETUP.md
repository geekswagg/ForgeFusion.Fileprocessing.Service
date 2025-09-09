# Development Setup Guide

This guide will help you set up the ForgeFusion File Processing Service for local development.

## Prerequisites

### Required Software
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js](https://nodejs.org/) (for Azurite)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) (for deployment)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (optional, for containers)
- [Git](https://git-scm.com/)

### Recommended IDEs
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Windows/Mac)
- [Visual Studio Code](https://code.visualstudio.com/) (Cross-platform)
- [JetBrains Rider](https://www.jetbrains.com/rider/) (Cross-platform)

## Quick Start

### 1. Clone Repository
```bash
git clone https://github.com/your-org/ForgeFusion.Fileprocessing.git
cd ForgeFusion.Fileprocessing
```

### 2. Install Azurite (Azure Storage Emulator)
```bash
npm install -g azurite
```

### 3. Start Azurite
```bash
# Linux/Mac
azurite --silent --location ./azurite-data --debug ./azurite-data/debug.log

# Windows
azurite --silent --location .\azurite-data --debug .\azurite-data\debug.log
```

### 4. Restore Dependencies
```bash
dotnet restore
```

### 5. Run Applications

#### Terminal 1 - API
```bash
cd ForgeFusion.Fileprocessing.Api
dotnet run
# API available at: https://localhost:7200
```

#### Terminal 2 - Web Application
```bash
cd ForgeFusion.Fileprocessing.Web
dotnet run
# Web app available at: https://localhost:7099
```

### 6. Open in Browser
- **Web Application**: https://localhost:7099
- **API Documentation**: https://localhost:7200/scalar

## Docker Development

### Run with Docker Compose
```bash
# Build and start all services
docker-compose up --build

# Run in background
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

### Access Applications
- **Web Application**: http://localhost:5001
- **API**: http://localhost:5000
- **Azurite Blob**: http://localhost:10000

## IDE Setup

### Visual Studio 2022
1. Open `ForgeFusion.Fileprocessing.Service.sln`
2. Set startup projects:
   - Right-click solution ? Properties
   - Select "Multiple startup projects"
   - Set both API and Web to "Start"
3. Press F5 to run both applications

### Visual Studio Code
1. Install recommended extensions:
   - C# Dev Kit
   - Azure Tools
   - Docker
2. Open workspace folder
3. Use tasks for running applications:
   ```bash
   # Run API
   Ctrl+Shift+P ? "Tasks: Run Task" ? "run-api"
   
   # Run Web
   Ctrl+Shift+P ? "Tasks: Run Task" ? "run-web"
   ```

## Configuration

### Local Development Settings

#### API (appsettings.Development.json)
```json
{
  "Storage": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "ContainerName": "files",
    "MaxFileSize": 209715200,
    "AllowedContentTypes": [
      "application/pdf",
      "image/png",
      "image/jpeg",
      "text/csv",
      "application/json"
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

#### Web (appsettings.Development.json)
```json
{
  "FileProcessingApi": {
    "BaseUrl": "https://localhost:7200"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Environment Variables
```bash
# Storage configuration
export Storage__ConnectionString="UseDevelopmentStorage=true"
export Storage__ContainerName="files"

# API URL for web app
export FileProcessingApi__BaseUrl="https://localhost:7200"
```

## Testing

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Categories
```bash
# Unit tests only
dotnet test --filter Category=Unit

# Integration tests only
dotnet test --filter Category=Integration

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test with Different Environments
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Testing
dotnet test

# Or inline
ASPNETCORE_ENVIRONMENT=Testing dotnet test
```

## Debugging

### Debug in Visual Studio
1. Set breakpoints in code
2. Press F5 or Debug ? Start Debugging
3. Both API and Web will start with debugging enabled

### Debug in Visual Studio Code
1. Set breakpoints
2. Press F5 or Run ? Start Debugging
3. Select configuration (API or Web)

### Debug with Docker
```bash
# Build debug images
docker-compose -f docker-compose.debug.yml up --build

# Attach debugger to running container
# Use VS Code Docker extension or Visual Studio Container Tools
```

## Troubleshooting

### Common Issues

#### "Connection refused" errors
- **Cause**: Azurite not running or wrong port
- **Solution**: Start Azurite and verify it's running on correct ports
  ```bash
  azurite --silent --location ./azurite-data
  ```

#### "Rejoining the server" in web app
- **Cause**: SignalR connection timeout during long operations
- **Solution**: Already configured with extended timeouts in Program.cs

#### File upload fails with validation errors
- **Cause**: File type not in allowed list
- **Solution**: Check `Storage:AllowedContentTypes` and `Storage:AllowedExtensions` in config

#### Docker containers can't communicate
- **Cause**: Network configuration or service discovery
- **Solution**: Check docker-compose.yml network settings and service names

### Debugging Tips

#### Enable Detailed Logging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Debug",
      "System": "Debug"
    }
  }
}
```

#### Monitor Azure Storage Operations
```bash
# View Azurite logs
tail -f ./azurite-data/debug.log
```

#### Check Application Health
```bash
# API health
curl https://localhost:7200/api/files

# Web app health
curl https://localhost:7099
```

## Performance Optimization

### Development Settings
- Use `ASPNETCORE_ENVIRONMENT=Development`
- Enable detailed errors and logging
- Use local storage emulator (Azurite)

### Local Performance Tips
- Allocate sufficient memory to Docker if using containers
- Use SSD storage for better I/O performance
- Close unnecessary applications during development

## Advanced Setup

### Custom Domain for Local Development
Add to hosts file:
```
127.0.0.1 forgefusion-api.local
127.0.0.1 forgefusion-web.local
```

Update launch settings to use custom domains.

### HTTPS with Custom Certificates
```bash
# Generate development certificate
dotnet dev-certs https --trust
```

### Database Migrations (if using Entity Framework)
```bash
# Add migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

## Next Steps

1. **Explore the Code**: Start with `Program.cs` files to understand startup
2. **Make Changes**: Try modifying file validation rules or adding new endpoints
3. **Run Tests**: Ensure your changes don't break existing functionality
4. **Deploy**: Use provided scripts to deploy to Azure

## Getting Help

- **Documentation**: Check the [README.md](../README.md) for project overview
- **API Reference**: Use `/scalar` endpoint for interactive API documentation
- **Issues**: Create GitHub issues for bugs or feature requests
- **Discussions**: Use GitHub discussions for questions and ideas

## Resources

- [.NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Azure Storage Documentation](https://docs.microsoft.com/en-us/azure/storage/)
- [Blazor Server Documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/)
- [Minimal APIs Documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)