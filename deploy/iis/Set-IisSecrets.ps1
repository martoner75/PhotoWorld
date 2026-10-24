<#
.SYNOPSIS
    Copies a project's user-secrets into IIS, as environment variables for that application only.

.DESCRIPTION
    Under IIS the apps run in Production as the app pool identity, so user-secrets (Development only, and stored in
    your own profile) aren't loaded. This script reads your secrets.json for the project and stores each value in IIS's
    own configuration (applicationHost.config, section system.webServer/aspNetCore/environmentVariables, locked to the
    one application). ASP.NET Core reads them at start-up; "Section:Key" becomes "Section__Key".

    The values never go into the published folder or web.config, so republishing doesn't remove them, and nothing is
    printed. Run it again after changing a secret. Needs an elevated PowerShell and the ASP.NET Core Hosting Bundle
    (which adds the aspNetCore section to IIS).

.EXAMPLE
    # In an administrator PowerShell, from the repository folder:
    .\deploy\iis\Set-IisSecrets.ps1 -Project Api -Site "PhotoWorld/API" -Extra @{ "Email:WebBaseUrl" = "https://photoworld/APP" }
    .\deploy\iis\Set-IisSecrets.ps1 -Project Web -Site "PhotoWorld/APP" -Extra @{ "PhotoWorldApi:BaseUrl" = "https://photoworld/API/" }
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet("Api", "Web")] [string] $Project,
    # The IIS site and application, as IIS Manager shows it: "Site/App".
    [Parameter(Mandatory)] [string] $Site,
    # Settings to add or override that aren't in user-secrets (for example addresses that differ under IIS).
    [hashtable] $Extra = @{},
    # Restart the application's pool afterwards so it picks the values up.
    [switch] $NoRecycle
)

$ErrorActionPreference = "Stop"
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this from an administrator PowerShell (right-click PowerShell → Run as administrator)."
}
Import-Module WebAdministration

$secretsIds = @{ Api = "2fd7292d-fe65-4f4a-9746-9e03cec2e3e4"; Web = "a9d0898f-f924-4114-8715-a7595f2e8a97" }
$file = Join-Path $env:APPDATA "Microsoft\UserSecrets\$($secretsIds[$Project])\secrets.json"
if (-not (Test-Path $file)) { throw "No user-secrets found for $Project at $file. Run it as the Windows user who set them." }

# secrets.json is flat: { "ConnectionStrings:PhotoWorld": "…", … }.
$settings = [ordered]@{}
(Get-Content $file -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $settings[$_.Name] = [string]$_.Value }
foreach ($key in $Extra.Keys) { $settings[$key] = [string]$Extra[$key] }

$section = "system.webServer/aspNetCore/environmentVariables"
$psPath = "MACHINE/WEBROOT/APPHOST"
if (-not (Get-WebConfiguration -PSPath $psPath -Location $Site -Filter "system.webServer/aspNetCore" -ErrorAction SilentlyContinue)) {
    throw "IIS doesn't know the aspNetCore section, or '$Site' doesn't exist. Install the ASP.NET Core Hosting Bundle, run iisreset, and check the name in IIS Manager."
}

foreach ($key in $settings.Keys) {
    $name = $key -replace ":", "__"
    # Replace any earlier value for this setting.
    if (Get-WebConfiguration -PSPath $psPath -Location $Site -Filter "$section/environmentVariable[@name='$name']") {
        Remove-WebConfigurationProperty -PSPath $psPath -Location $Site -Filter $section -Name "." -AtElement @{ name = $name }
    }
    Add-WebConfigurationProperty -PSPath $psPath -Location $Site -Filter $section -Name "." `
        -Value @{ name = $name; value = $settings[$key] }
    Write-Host "Set $name"
}

if (-not $NoRecycle) {
    $pool = (Get-WebApplication -Site ($Site -split "/")[0] -Name ($Site -split "/", 2)[1]).applicationPool
    Restart-WebAppPool -Name $pool
    Write-Host "Recycled app pool $pool."
}
Write-Host "Done: $($settings.Count) settings stored for $Site (values not shown)."
