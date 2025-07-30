# Generator - .NET 8 Console Application

A simple "Hello World" .NET 8 console application with Azure Storage integration capabilities.

## Features

- .NET 8 console application
- Configuration management using JSON files
- Azure Storage Blob client integration
- Comprehensive logging
- Error handling with retry logic
- Security best practices

## Prerequisites

- .NET 8.0 SDK
- Azure Storage Account (for storage testing)

## Configuration

### Setup Azure Storage Connection

1. Copy `generator.config.json.template` to `generator.config.json`
2. Replace the placeholder values:
   - `<your-storage-account-name>`: Your Azure Storage account name
   - `<your-account-key>`: Your Azure Storage account key

### Security Best Practices

**Development Environment:**
- Use connection strings with account keys for local development
- Never commit real connection strings to source control
- Use the template file for reference

**Production Environment:**
- Use Azure Managed Identity instead of connection strings
- Store secrets in Azure Key Vault
- Implement proper RBAC for storage access

## Usage

### Build the Application

```bash
dotnet build
```

### Run the Application

```bash
dotnet run
```

### Expected Output

```
Generator Application Starting...
Configuration loaded successfully
Hello World from Generator!
=================================
Testing Azure Storage connection...
✓ Azure Storage connection test successful!
  Account Kind: StorageV2
  SKU Name: Standard_LRS
Generator Application Completed Successfully
```

## Project Structure

```
generator/
├── generator.csproj              # Project file with dependencies
├── Program.cs                    # Main application code
├── generator.config.json         # Configuration file (user-specific)
├── generator.config.json.template # Configuration template
└── README.md                     # This file
```

## Dependencies

- **Azure.Storage.Blobs** (12.21.2): Azure Blob Storage client library
- **Azure.Identity** (1.12.1): Azure authentication library
- **Microsoft.Extensions.Configuration** (8.0.0): Configuration framework
- **Microsoft.Extensions.Logging** (8.0.1): Logging framework

## Azure Storage Integration

The application includes optional Azure Storage connectivity testing:

- Loads connection string from configuration
- Tests connection using Azure Storage Blob client
- Displays account information if connection is successful
- Gracefully handles connection failures

## Error Handling

- Comprehensive exception handling
- Structured logging with different log levels
- Graceful degradation when Azure Storage is not configured
- Exit codes for automation scenarios

## Security Considerations

1. **Never hardcode credentials** in source code
2. **Use Azure Key Vault** for production secrets
3. **Prefer Managed Identity** over connection strings
4. **Implement least privilege** access policies
5. **Rotate keys regularly**
6. **Enable encryption** in transit and at rest

## Next Steps

To extend this application:

1. Add specific generator functionality for your use case
2. Implement Azure Storage operations (upload, download, list)
3. Add configuration for multiple environments
4. Integrate with Azure Key Vault for secret management
5. Add unit tests and integration tests
