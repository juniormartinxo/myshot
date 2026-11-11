# Gera o par de chaves de atualização e assina o manifesto da release.
#   -GenerateKey : cria a chave privada fora do repositório e grava a chave pública em assets\update-public-key.xml
#   (padrão)     : gera artifacts\MyShot.manifest e artifacts\MyShot.manifest.sig a partir de artifacts\MyShot.exe
# A chave privada nunca entra no repositório. Guarde uma cópia em local seguro: sem ela não há como assinar novas versões.
param(
    [switch]$GenerateKey,
    [string]$KeyDir = (Join-Path $env:USERPROFILE '.myshot-keys'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$privatePath = Join-Path $KeyDir 'myshot-update-private.xml'
$publicPath = Join-Path $root 'assets\update-public-key.xml'

if ($GenerateKey) {
    if (Test-Path $privatePath) { throw "A chave privada já existe em $privatePath; ela não será sobrescrita." }
    New-Item -ItemType Directory -Force $KeyDir | Out-Null
    $rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider 2048
    [IO.File]::WriteAllText($privatePath, $rsa.ToXmlString($true), (New-Object Text.UTF8Encoding $false))
    [IO.File]::WriteAllText($publicPath, $rsa.ToXmlString($false), (New-Object Text.UTF8Encoding $false))
    "Chave privada: $privatePath (guarde uma cópia em local seguro, fora do repositório)"
    "Chave pública: $publicPath (commite este arquivo)"
    return
}

$source = Get-Content (Join-Path $root 'src\Updater.cs') -Raw
$version = [regex]::Match($source, 'Version="([0-9.]+)"').Groups[1].Value
if (-not $version) { throw 'Versão não encontrada em src\Updater.cs.' }
$exe = Join-Path $OutputDirectory 'MyShot.exe'
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash.ToLowerInvariant()
$size = (Get-Item -LiteralPath $exe).Length
$manifestPath = Join-Path $OutputDirectory 'MyShot.manifest'
[IO.File]::WriteAllText($manifestPath, "version=$version`nsha256=$hash`nsize=$size`n", (New-Object Text.UTF8Encoding $false))

if (-not (Test-Path $privatePath)) { throw "Chave privada ausente em $privatePath. Rode com -GenerateKey na primeira vez." }
$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
$rsa.FromXmlString([IO.File]::ReadAllText($privatePath))
$signature = $rsa.SignData([IO.File]::ReadAllBytes($manifestPath), 'SHA256')
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'MyShot.manifest.sig'), [Convert]::ToBase64String($signature))
"Manifesto assinado: versão $version, $size bytes, SHA-256 $hash"
