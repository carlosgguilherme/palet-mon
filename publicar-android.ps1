# Assina no PC o APK que o GitHub compilou e anexa à release. A chave não sai deste computador.
# Rode depois que o workflow Release da tag terminar:
#
#   .\publicar-android.ps1 -Versao 2.2.0
param([Parameter(Mandatory = $true)] [string] $Versao)

$ErrorActionPreference = "Stop"
$gh = "C:\Program Files\GitHub CLI\gh.exe"
$tag = "v$Versao"
$chaves = Join-Path $PSScriptRoot "chaves"
$jks = Join-Path $chaves "paletmon-release.jks"
$senha = Join-Path $chaves "senha.txt"
if (-not (Test-Path $jks)) { throw "Chave não encontrada em $jks" }

# apksigner vem das ferramentas oficiais do Android, baixadas uma vez
$ferramentas = Join-Path $chaves "build-tools"
$assinador = Get-ChildItem $ferramentas -Recurse -Filter apksigner.jar -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $assinador) {
    New-Item -ItemType Directory -Force $ferramentas | Out-Null
    $zip = Join-Path $ferramentas "build-tools.zip"
    curl.exe -sL -o $zip "https://dl.google.com/android/repository/build-tools_r34-windows.zip"
    Expand-Archive $zip -DestinationPath $ferramentas -Force
    Remove-Item $zip
    $assinador = Get-ChildItem $ferramentas -Recurse -Filter apksigner.jar | Select-Object -First 1
}

$run = (& $gh run list --workflow Release --branch $tag --limit 1 --json databaseId | ConvertFrom-Json)[0].databaseId
if (-not $run) { throw "Não encontrei o workflow Release da tag $tag." }

$pasta = Join-Path $env:TEMP "paletmon-apk-$Versao"
Remove-Item $pasta -Recurse -Force -ErrorAction SilentlyContinue
& $gh run download $run -n apk-sem-chave -D $pasta
if ($LASTEXITCODE -ne 0) { throw "Não consegui baixar o APK do workflow $run." }

$entrada = Join-Path $pasta "Palet-Mon-Android.apk"
$saida = Join-Path $pasta "assinado\Palet-Mon-Android.apk"
New-Item -ItemType Directory -Force (Split-Path $saida) | Out-Null
& java -jar $assinador.FullName sign --ks $jks --ks-type PKCS12 --ks-key-alias paletmon --ks-pass "file:$senha" --out $saida $entrada
if ($LASTEXITCODE -ne 0) { throw "A assinatura falhou." }
& java -jar $assinador.FullName verify --print-certs $saida | Select-String "certificate DN|SHA-256"
if ($LASTEXITCODE -ne 0) { throw "A verificação da assinatura falhou." }

& $gh release upload $tag $saida --clobber
if ($LASTEXITCODE -ne 0) { throw "Não consegui anexar o APK à release." }
Write-Host "APK publicado em $tag"
