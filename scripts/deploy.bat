@echo off
REM ForgeFusion File Processing Service - Azure Deployment Script (Windows)
REM Usage: deploy.bat [environment] [resource-group-name] [subscription-id] [location]

setlocal enabledelayedexpansion

set ENVIRONMENT=%1
if "%ENVIRONMENT%"=="" set ENVIRONMENT=dev

set RESOURCE_GROUP=%2
if "%RESOURCE_GROUP%"=="" set RESOURCE_GROUP=rg-forgefusion-%ENVIRONMENT%

set SUBSCRIPTION_ID=%3
set LOCATION=%4
if "%LOCATION%"=="" set LOCATION=eastus

echo ?? Deploying ForgeFusion File Processing Service
echo Environment: %ENVIRONMENT%
echo Resource Group: %RESOURCE_GROUP%
echo Location: %LOCATION%

REM Set Azure subscription if provided
if not "%SUBSCRIPTION_ID%"=="" (
    echo Setting subscription to: %SUBSCRIPTION_ID%
    az account set --subscription "%SUBSCRIPTION_ID%"
    if errorlevel 1 goto :error
)

REM Create resource group if it doesn't exist
echo ?? Creating resource group...
az group create --name "%RESOURCE_GROUP%" --location "%LOCATION%"
if errorlevel 1 goto :error

REM Deploy infrastructure
echo ??? Deploying infrastructure...
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "DEPLOYMENT_NAME=forgefusion-infrastructure-%dt:~0,8%-%dt:~8,6%"

az deployment group create ^
    --resource-group "%RESOURCE_GROUP%" ^
    --template-file "infrastructure/main.bicep" ^
    --parameters "@infrastructure/parameters.%ENVIRONMENT%.json" ^
    --name "%DEPLOYMENT_NAME%" ^
    --verbose
if errorlevel 1 goto :error

REM Get deployment outputs
echo ?? Getting deployment outputs...
for /f %%i in ('az deployment group show --resource-group "%RESOURCE_GROUP%" --name "%DEPLOYMENT_NAME%" --query properties.outputs.storageAccountName.value -o tsv') do set STORAGE_ACCOUNT_NAME=%%i
for /f %%i in ('az deployment group show --resource-group "%RESOURCE_GROUP%" --name "%DEPLOYMENT_NAME%" --query properties.outputs.apiAppName.value -o tsv') do set API_APP_NAME=%%i
for /f %%i in ('az deployment group show --resource-group "%RESOURCE_GROUP%" --name "%DEPLOYMENT_NAME%" --query properties.outputs.webAppName.value -o tsv') do set WEB_APP_NAME=%%i
for /f %%i in ('az deployment group show --resource-group "%RESOURCE_GROUP%" --name "%DEPLOYMENT_NAME%" --query properties.outputs.apiAppUrl.value -o tsv') do set API_APP_URL=%%i
for /f %%i in ('az deployment group show --resource-group "%RESOURCE_GROUP%" --name "%DEPLOYMENT_NAME%" --query properties.outputs.webAppUrl.value -o tsv') do set WEB_APP_URL=%%i

echo ? Infrastructure deployed successfully!
echo.
echo ?? Deployment Summary:
echo Storage Account: %STORAGE_ACCOUNT_NAME%
echo API App: %API_APP_NAME%
echo Web App: %WEB_APP_NAME%
echo API URL: %API_APP_URL%
echo Web URL: %WEB_APP_URL%
echo.

REM Check if we should skip app deployment
if "%5"=="--skip-apps" (
    echo ?? Skipping application deployment
    goto :end
)

REM Build and deploy applications
echo ?? Building applications...

REM Build API
echo Building API...
dotnet publish ForgeFusion.Fileprocessing.Api/ForgeFusion.Fileprocessing.Api.csproj ^
    --configuration Release ^
    --output ./publish/api
if errorlevel 1 goto :error

REM Build Web
echo Building Web...
dotnet publish ForgeFusion.Fileprocessing.Web/ForgeFusion.Fileprocessing.Web.csproj ^
    --configuration Release ^
    --output ./publish/web
if errorlevel 1 goto :error

REM Create deployment packages
echo ?? Creating deployment packages...
powershell -Command "Compress-Archive -Path './publish/api/*' -DestinationPath './publish/api.zip' -Force"
powershell -Command "Compress-Archive -Path './publish/web/*' -DestinationPath './publish/web.zip' -Force"

REM Deploy API
echo ?? Deploying API...
az webapp deployment source config-zip ^
    --resource-group "%RESOURCE_GROUP%" ^
    --name "%API_APP_NAME%" ^
    --src "./publish/api.zip"
if errorlevel 1 goto :error

REM Deploy Web
echo ?? Deploying Web...
az webapp deployment source config-zip ^
    --resource-group "%RESOURCE_GROUP%" ^
    --name "%WEB_APP_NAME%" ^
    --src "./publish/web.zip"
if errorlevel 1 goto :error

REM Warm up applications
echo ?? Warming up applications...
timeout /t 30 /nobreak >nul

curl -f "%API_APP_URL%/api/files" || echo ?? API warmup failed
curl -f "%WEB_APP_URL%" || echo ?? Web warmup failed

REM Cleanup
echo ?? Cleaning up...
rmdir /s /q "./publish" 2>nul

echo.
echo ?? Deployment completed successfully!
echo.
echo ?? Application URLs:
echo API: %API_APP_URL%
echo Web: %WEB_APP_URL%
echo API Documentation: %API_APP_URL%/scalar
echo.
echo ?? Next steps:
echo 1. Test the applications using the URLs above
echo 2. Configure custom domains if needed
echo 3. Set up monitoring alerts
echo 4. Review security settings

goto :end

:error
echo ? Deployment failed with error level %errorlevel%
exit /b %errorlevel%

:end
endlocal