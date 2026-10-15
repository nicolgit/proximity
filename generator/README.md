# Generator - Metro Proximity Generator

A .NET 10 console application for managing areas and generating station proximity data using Azure Maps or MapBox APIs.

## Prerequisites

- .NET 10.0 SDK
- Azure Storage Account (for area storage, station data, and isochrone polygons)
- MapBox API Key (for geographical services and isochrone generation)

## Configuration and authentication

Copy `generator.config.template.json` to `generator.config.json` in the generator directory and set the MapBox key and storage account URLs. Run the generator from this directory: configuration is loaded from the current working directory.

Azure Storage authentication supports two options:

### Azure CLI (local development, no application secret)

Install Azure CLI and sign in with the identity that should access Storage:

```powershell
az login --tenant "<tenant-id>"
az account set --subscription "<subscription-id>"
```

In `AppSettings`, omit all three credential settings (`tenantId`, `clientId`, and `clientSecret`), or set them to null or empty strings. For example, replace the template's credential placeholders with:

```json
"tenantId": "",
"clientId": "",
"clientSecret": ""
```

The generator uses `AzureCliCredential` and Azure CLI's current tenant and login, not automatically the Windows or VPN session. All three credential settings must be absent, null, empty, or whitespace for this mode. Template placeholders are not treated as missing values.

### Service principal (application credentials)

Set all three values in `AppSettings`:

```json
"tenantId": "<tenant-id>",
"clientId": "<application-client-id>",
"clientSecret": "<client-secret-value>"
```

The generator uses `ClientSecretCredential`. Use the secret's **value**, not its ID. If any credential setting is supplied, all three are required; partial configuration fails explicitly. An authentication failure does not fall back to another identity.

The infrastructure deployment creates **proximity-generator-app** and assigns its service principal the required Storage data roles. Use the deployment's `generatorTenantId` and `generatorClientId` outputs, then create a client secret as described in the [deployment guide](../deployment/readme.md#generator-application-identity). The template does not generate a secret; the Entra identity requires separate cleanup when the resource group is deleted.

For either option, grant the selected identity (your Azure CLI user or the service principal) **Storage Blob Data Contributor** and **Storage Table Data Contributor** on the target storage account. Private endpoints also require network connectivity and DNS resolution; VPN authentication does not grant Storage data permissions.

Do not commit `generator.config.json` containing secrets. The generator currently reads this JSON file only; environment variables and .NET User Secrets are not loaded.

## Quick Reference

```bash
# Area Management
dotnet run -- area create italy/rome --center "41.9028,12.4964" --diameter 1000 --displayname "Rome City Center"
dotnet run -- area create italy/rome --center "40,40" --diameter 1000 --displayname "Test Area" --developer --logging Debug
dotnet run -- area isochrone italy/rome
dotnet run -- area isochrone italy/rome --delete
dotnet run -- area delete "italy/rome"
dotnet run -- area list

# Station Management  
dotnet run -- station list italy/rome
dotnet run -- station list rome --filter Roma
dotnet run -- station isochrone italy/rome
dotnet run -- station isochrone italy/milan --delete
dotnet run -- station isochrone italy/milan 21226369
dotnet run -- station isochrone italy/milan 21226369 --delete
dotnet run -- station isochrone italy/milan 21226369 --delete 10

# Global Options
dotnet run -- --logging Debug
dotnet run -- --help
```
 