#!/bin/bash

# Create Entra ID Applications Script (Azure CLI)
# This script creates the three Entra ID applications with proper permissions for Azure Storage access

set -e

# Parameters
TENANT_ID=${1:-""}
APPLICATION_NAME_PREFIX=${2:-"proximity"}

if [ -z "$TENANT_ID" ]; then
    echo "Usage: $0 <tenant-id> [application-name-prefix]"
    echo "Example: $0 12345678-1234-1234-1234-123456789012 proximity"
    exit 1
fi

# Ensure we're logged into Azure CLI
echo "Checking Azure CLI login status..."
if ! az account show &>/dev/null; then
    echo "Please login to Azure CLI first: az login"
    exit 1
fi

# Set the tenant
az account set --subscription $(az account list --query "[?tenantId=='$TENANT_ID'].id" -o tsv | head -1)

echo "Creating Entra ID applications for storage access..."

# Define the applications
declare -A APPLICATIONS=(
    ["${APPLICATION_NAME_PREFIX}-storage-reader"]="Application for read-only access to Proximity storage account tables and blobs"
    ["${APPLICATION_NAME_PREFIX}-storage-writer"]="Application for write access to Proximity storage account tables and blobs"
    ["${APPLICATION_NAME_PREFIX}-storage-contributor"]="Application for full contributor access to Proximity storage account"
)

# Azure Storage Resource App ID
STORAGE_RESOURCE_APP_ID="e406a681-f3d4-42a8-90b6-c2b029497af1"

for app_name in "${!APPLICATIONS[@]}"; do
    description="${APPLICATIONS[$app_name]}"
    
    echo "Creating application: $app_name"
    
    # Check if app already exists
    existing_app=$(az ad app list --display-name "$app_name" --query "[0].appId" -o tsv 2>/dev/null || echo "")
    
    if [ -n "$existing_app" ]; then
        echo "✓ Application '$app_name' already exists with App ID: $existing_app"
        continue
    fi
    
    # Create the application
    app_id=$(az ad app create \
        --display-name "$app_name" \
        --sign-in-audience "AzureADMyOrg" \
        --query "appId" -o tsv)
    
    if [ $? -eq 0 ]; then
        # Create service principal
        sp_object_id=$(az ad sp create --id "$app_id" --query "id" -o tsv)
        
        # Add required resource access for Azure Storage
        az ad app permission add \
            --id "$app_id" \
            --api "$STORAGE_RESOURCE_APP_ID" \
            --api-permissions "03e0da56-190b-40ad-a80c-ea378c433f7f=Scope"
        
        echo "✓ Created application '$app_name'"
        echo "  App ID: $app_id"
        echo "  Service Principal Object ID: $sp_object_id"
        echo ""
    else
        echo "✗ Failed to create application '$app_name'"
    fi
done

echo "Application creation completed!"
echo ""
echo "Next steps:"
echo "1. Grant admin consent for the API permissions:"
echo "   az ad app permission admin-consent --id <app-id>"
echo "2. Deploy your Bicep template to create managed identities and RBAC assignments"
echo "3. If you prefer to use these Entra applications instead of managed identities,"
echo "   update your Bicep template to reference these application IDs"