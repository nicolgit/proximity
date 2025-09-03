# Azure Storage Integration

This Azure Functions application includes secure Azure Storage integration with connection testing on startup.

## Configuration

### Local Development

Update your `local.settings.json` file:

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "CustomStorageConnectionString": "UseDevelopmentStorage=true"
    }
}
```

For local development with Azure Storage Emulator, use `"UseDevelopmentStorage=true"`.

### Production Deployment

For production, replace the connection string with your actual Azure Storage account:

```json
{
    "CustomStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=yourkey;EndpointSuffix=core.windows.net"
}
```

**Security Best Practices:**
- Use Application Settings in Azure Functions App instead of hardcoding connection strings
- Consider using Managed Identity instead of connection strings for enhanced security
- Store sensitive configuration in Azure Key Vault

### Using Managed Identity (Recommended for Production)

To use Managed Identity instead of connection strings:

1. Enable System Assigned Managed Identity on your Azure Functions App
2. Grant the Managed Identity "Storage Blob Data Contributor" role on your Storage Account
3. Set the connection string to your storage account URL:
   ```
   "CustomStorageConnectionString": "https://yourstorageaccount.blob.core.windows.net"
   ```

## Features

### Startup Health Check

The application automatically tests the Azure Storage connection when it starts up. You'll see logs like:

```
[Information] Starting application health checks...
[Information] Testing Azure Storage connection...
[Information] Azure Storage connection test successful. Service version: 2021-12-02
[Information] ✅ Azure Storage connection test passed
[Information] Application health checks completed. Storage: Healthy
```

### Available Endpoints

#### 1. Health Check
- **GET** `/api/Hello`
- Returns application status including storage connection health

#### 2. Storage Health
- **GET** `/api/StorageHealth`
- Returns detailed storage connection status

#### 3. Create Container
- **POST** `/api/CreateContainer`
- Body: `{"ContainerName": "mycontainer"}`
- Creates a new blob container if it doesn't exist

#### 4. Upload Text
- **POST** `/api/UploadText`
- Body: 
  ```json
  {
    "ContainerName": "mycontainer",
    "BlobName": "myfile.txt",
    "Content": "Hello, Azure Storage!"
  }
  ```
- Uploads text content as a blob

## Usage Examples

### Test Storage Connection
```bash
curl -X GET http://localhost:7071/api/StorageHealth
```

### Create a Container
```bash
curl -X POST http://localhost:7071/api/CreateContainer \
  -H "Content-Type: application/json" \
  -d '{"ContainerName": "testcontainer"}'
```

### Upload Text File
```bash
curl -X POST http://localhost:7071/api/UploadText \
  -H "Content-Type: application/json" \
  -d '{
    "ContainerName": "testcontainer",
    "BlobName": "sample.txt",
    "Content": "This is a test file uploaded to Azure Storage!"
  }'
```

## Architecture

### StorageService
- **Purpose**: Centralized Azure Storage operations with secure connection handling
- **Features**: 
  - Automatic connection string vs managed identity detection
  - Connection testing capabilities
  - Container management
  - Comprehensive error handling and logging

### StartupHealthCheckService
- **Purpose**: Validates external dependencies on application startup
- **Features**:
  - Tests Azure Storage connectivity
  - Logs startup health status
  - Non-blocking startup (warnings only if connections fail)

## Error Handling

The implementation includes comprehensive error handling:

- **Connection Failures**: Logged with detailed error messages
- **Invalid Requests**: Return appropriate HTTP status codes
- **Service Exceptions**: Caught and logged with proper error responses
- **Startup Issues**: Logged as warnings to allow application to start

## Logging

All operations include structured logging with:
- Connection test results
- Container operations
- Upload/download activities
- Error details with context

## Security Considerations

1. **Managed Identity**: Preferred authentication method for production
2. **Connection String Security**: Never commit actual connection strings to source control
3. **Least Privilege**: Grant minimal required permissions to storage accounts
4. **Encryption**: All connections use HTTPS/TLS encryption
5. **Access Logging**: All storage operations are logged for audit purposes

## Development Setup

1. Install Azure Storage Emulator or use Azure Storage Account
2. Update `local.settings.json` with appropriate connection string
3. Run the application: `func start` or `dotnet run`
4. Check logs for successful storage connection test

## Troubleshooting

### Common Issues

1. **"CustomStorageConnectionString is not configured"**
   - Ensure the setting exists in local.settings.json or App Settings

2. **Connection test fails with Azurite**
   - Make sure Azure Storage Emulator (Azurite) is running
   - Verify the connection string format

3. **Managed Identity authentication fails**
   - Ensure the Function App has System Assigned Managed Identity enabled
   - Verify RBAC permissions on the Storage Account

4. **Network connectivity issues**
   - Check firewall settings
   - Verify storage account network access rules
