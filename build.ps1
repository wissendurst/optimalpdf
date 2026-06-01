$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root "dist"
$appOut = Join-Path $dist "OptimalPDFReaderApp"
$installerOut = Join-Path $dist "Installer"
$payload = Join-Path $root "installer\Payload.zip"

if (Test-Path $dist) {
    Remove-Item -LiteralPath $dist -Recurse -Force
}

New-Item -ItemType Directory -Force $dist | Out-Null

dotnet publish (Join-Path $root "src\SimplePdfReaderWin.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $appOut

Get-ChildItem $appOut -Recurse -Include *.xml,*.pdb | Remove-Item -Force

if (Test-Path $payload) {
    Remove-Item -LiteralPath $payload -Force
}

Compress-Archive -Path (Join-Path $appOut "*") -DestinationPath $payload

dotnet publish (Join-Path $root "installer\OptimalPdfReaderInstaller.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $installerOut

Copy-Item (Join-Path $installerOut "OptimalPDFReaderSetup.exe") (Join-Path $dist "OptimalPDFReaderSetup.exe") -Force
Remove-Item -LiteralPath $installerOut -Recurse -Force
Remove-Item -LiteralPath $payload -Force

Write-Host "Built $dist\OptimalPDFReaderSetup.exe"
