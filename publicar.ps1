# Gera o instalador do Palet Mon em Releases\, sem publicar nada.
#
#   .\publicar.ps1 -Versao 2.2.0
#   .\publicar.ps1 -Versao 2.2.0 -Certificado <impressão digital do certificado>
param(
    [Parameter(Mandatory = $true)] [string] $Versao,
    [string] $Certificado
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
Set-Location $PSScriptRoot
$saida = "dist\publish"

dotnet tool restore
dotnet publish PaletMon\PaletMon.csproj -c Release -r win-x64 --self-contained true "-p:Version=$Versao" -p:DebugType=none -o $saida
if ($LASTEXITCODE -ne 0) { throw "A compilação falhou." }

$pack = @("vpk", "pack", "-u", "PaletMonApp", "-v", $Versao, "-p", $saida, "-e", "PaletMon.exe", "--packTitle", "Palet Mon", "--packAuthors", "Palet Mon", "--icon", "PaletMon\Assets\PaletMon.ico")
if ($Certificado) { $pack += @("--signParams", "/sha1 $Certificado /fd sha256 /tr http://timestamp.digicert.com /td sha256") }
dotnet @pack
if ($LASTEXITCODE -ne 0) { throw "O empacotamento falhou." }

Get-ChildItem Releases | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }
