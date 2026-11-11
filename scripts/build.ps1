param([switch]$Test,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { throw 'Instale o .NET Framework 4.8 no Windows x64.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts' }
if (![IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path (Get-Location).ProviderPath $OutputDirectory }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName)
$resources = @(Get-ChildItem (Join-Path $root 'assets/icons') -Filter *.svg | ForEach-Object { "/resource:$($_.FullName),MyShot.icons.$($_.Name)" })
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ "/out:$OutputDirectory\MyShot.exe" "/win32manifest:$root\src\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll $sources $resources
if ($LASTEXITCODE -ne 0) { throw 'Falha de compilação.' }
Copy-Item -LiteralPath "$root\src\App.config" -Destination "$OutputDirectory\MyShot.exe.config" -Force
# Checksum publicado junto da release; o recurso de atualização recusa o executável que não bater com ele.
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath "$OutputDirectory\MyShot.exe").Hash.ToLowerInvariant()
Set-Content -LiteralPath "$OutputDirectory\MyShot.exe.sha256" -Value "$hash  MyShot.exe" -Encoding ascii
if ($Test) {
    & $compiler /nologo /target:exe /platform:x64 /warnaserror+ "/out:$OutputDirectory\MyShot.Tests.exe" /main:MyShot.Tests "/win32manifest:$root\src\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll $sources $resources "$root\tests\Tests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar testes.' }
    Copy-Item -LiteralPath "$root\src\App.config" -Destination "$OutputDirectory\MyShot.Tests.exe.config" -Force
    & "$OutputDirectory\MyShot.Tests.exe" "$root\research\private\validation"
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam.' }
}
# Instalador: um único .exe com o app embutido; copia para %LOCALAPPDATA%\Programs\MyShot e cria o atalho do menu Iniciar.
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ "/out:$OutputDirectory\MyShot-Setup.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/resource:$OutputDirectory\MyShot.exe,MyShot.exe" "/resource:$OutputDirectory\MyShot.exe.config,MyShot.exe.config" "$root\installer\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }
Get-Item "$OutputDirectory\MyShot.exe" | Select-Object FullName,Length
