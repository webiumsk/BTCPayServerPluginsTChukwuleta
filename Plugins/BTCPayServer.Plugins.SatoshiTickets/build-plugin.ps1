# Build and pack SatoshiTickets as an installable BTCPay Server plugin (.btcpay)
param([string]$OutputDir = "")

$ErrorActionPreference = "Stop"
$PluginName = "BTCPayServer.Plugins.SatoshiTickets"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$BuildDir = Join-Path $ScriptDir "bin\Release\net8.0"
$OutputDir = if ($OutputDir) { $OutputDir } else { Join-Path $RepoRoot "dist" }

Write-Host "Building $PluginName..."
dotnet build (Join-Path $ScriptDir "$PluginName.csproj") -c Release

Write-Host "Packing plugin..."
Push-Location (Join-Path $RepoRoot "btcpayserver\BTCPayServer.PluginPacker")
dotnet run -- $BuildDir $PluginName $OutputDir
Pop-Location

$VersionDir = Get-ChildItem (Join-Path $OutputDir $PluginName) -Directory | Select-Object -First 1
Write-Host ""
Write-Host "Done! Installable plugin created at:"
Write-Host "  $(Join-Path $VersionDir.FullName "$PluginName.btcpay")"
Write-Host ""
Write-Host "To install: Upload this file via BTCPay Server > Settings > Plugins"
