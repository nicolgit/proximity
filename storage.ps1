# ./storage.ps1 -StorageAccountName <your-storage-account> -ResourceGroupName <your-resource-group>

param(
	[Parameter(Mandatory = $true)]
	[string]$StorageAccountName,
	[Parameter(Mandatory = $true)]
	[string]$ResourceGroupName,
	[Parameter(Mandatory = $true)]
	[string]$TenantId,
	[Parameter(Mandatory = $true)]
	[string]$SubscriptionId
)

Install-Module -Name Az.Storage
Import-Module Az.Storage

Connect-AzAccount -Tenant $TenantId -Subscription $SubscriptionId

# Enable storage account key access and public network access
$storageAccount = Get-AzStorageAccount -ResourceGroupName $ResourceGroupName -Name $StorageAccountName

# Enable public network access and shared key access
Update-AzStorageAccount `
	-ResourceGroupName $ResourceGroupName `
	-Name $StorageAccountName `
	-AllowBlobPublicAccess $true `
	-AllowSharedKeyAccess $true `
	-PublicNetworkAccess Enabled

Write-Output "Storage account '$StorageAccountName' updated: Key access and public network access enabled."