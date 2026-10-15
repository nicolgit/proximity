#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$SubscriptionId,
    [string]$ResourceGroupName = 'hub-and-spoke-playground',
    [string]$GatewayName = 'lab-gateway'
)

$ErrorActionPreference = 'Stop'
$workDirectory = Join-Path $PWD.Path ('proximity-vpn-' + [guid]::NewGuid())
$archivePath = "$workDirectory.zip"
New-Item -ItemType Directory -Path $workDirectory | Out-Null

try {
    Write-Host 'Generating and downloading the VPN profile...'
    $result = az network vnet-gateway vpn-client generate `
        --subscription $SubscriptionId --resource-group $ResourceGroupName --name $GatewayName `
        --only-show-errors --output json
    if ($LASTEXITCODE -ne 0) { throw "VPN profile generation failed (exit code $LASTEXITCODE)." }
    $downloadUrl = $result -join "`n" | ConvertFrom-Json
    $extractPath = Join-Path $workDirectory 'profile'
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath -UseBasicParsing
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath

    $profiles = @(Get-ChildItem -LiteralPath $extractPath -Recurse -Filter 'azurevpnconfig_aad.xml' -File)
    if ($profiles.Count -eq 0) {
        $profiles = @(Get-ChildItem -LiteralPath $extractPath -Recurse -Filter 'azurevpnconfig.xml' -File)
    }
    if ($profiles.Count -ne 1) { throw "Expected one Entra VPN profile; found $($profiles.Count)." }
    [xml]$profile = Get-Content -LiteralPath $profiles[0].FullName -Raw
    $name = 'proximity enviroment'
    $servers = @($profile.AzVpnProfile.serverlist.ServerEntry)
    if ([string]::IsNullOrWhiteSpace([string]$profile.AzVpnProfile.name) -or $servers.Count -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$servers[0].fqdn)) {
        throw 'Expected a VPN profile with a connection name and exactly one server FQDN.'
    }
    $profile.AzVpnProfile.name = $name

    # WinGet returns 0x8A15002B when no newer version is available.
    foreach ($operation in @('install', 'upgrade')) {
        Write-Host "Running WinGet $operation for Azure VPN Client..."
        winget $operation --id 9NP355QT2SQB --exact --source msstore --accept-source-agreements --accept-package-agreements
        if ($LASTEXITCODE -eq -1978335189) {
            Write-Host 'Azure VPN Client is already up to date.'
        }
        elseif ($LASTEXITCODE -ne 0) { throw "WinGet $operation failed (exit code $LASTEXITCODE)." }
    }
    $package = Get-AppxPackage -Name 'Microsoft.AzureVpn'
    if (-not $package) { throw 'Azure VPN Client is not registered for the current user.' }

    $settings = @{
        Name = $name
        ServerAddress = [string]$servers[0].fqdn
        PlugInApplicationID = $package.PackageFamilyName
        CustomConfiguration = $profile
        SplitTunneling = $true
    }
    $existing = Get-VpnConnection | Where-Object { $_.Name -eq $name }
    if ($existing) {
        if ($existing.PlugInApplicationID -ne $package.PackageFamilyName) {
            throw "Connection '$name' belongs to another VPN provider; it will not be overwritten."
        }
        if ($existing.ConnectionStatus -ne 'Disconnected') { throw "Disconnect '$name' before updating it." }
        Set-VpnConnection @settings -ThirdPartyVpn -Force
    }
    else {
        Add-VpnConnection @settings
    }

    $connection = Get-VpnConnection -Name $name
    if ($connection.PlugInApplicationID -ne $settings.PlugInApplicationID -or
        $connection.ServerAddress -ne $settings.ServerAddress -or -not $connection.SplitTunneling -or
        $null -eq $connection.CustomConfiguration -or
        $connection.CustomConfiguration.DocumentElement.OuterXml -cne $profile.DocumentElement.OuterXml) {
        throw "VPN profile verification failed for '$name'."
    }
    Start-Process explorer.exe -ArgumentList "shell:AppsFolder\$($package.PackageFamilyName)!App"
    Write-Host "VPN connection '$name' registered and verified. Reopen Azure VPN Client if the list has not refreshed."
    Write-Host "Select Connect and complete Entra sign-in/MFA, then run the connectivity checks in step 6."
}
finally {
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
    Remove-Item -LiteralPath $workDirectory -Recurse -Force
}
