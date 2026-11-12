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
$resources += "/resource:$root\assets\update-public-key.xml,MyShot.update-public-key.xml"
$resources += "/resource:$root\assets\myshot.ico,MyShot.myshot.ico"
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ "/out:$OutputDirectory\MyShot.exe" "/win32manifest:$root\src\app.manifest" "/win32icon:$root\assets\myshot.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll $sources $resources
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
# Instalador (Inno Setup 6): um único MyShot-Setup.exe com o app embutido; instala por usuário, sem administrador.
$iscc = @(
    $env:ISCC,
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (!$iscc) { throw 'Instale o Inno Setup 6 (winget install JRSoftware.InnoSetup) ou defina a variável ISCC com o caminho do ISCC.exe.' }
$appVersion = [regex]::Match((Get-Content -Raw (Join-Path $root 'src\Updater.cs')), 'Version="([^"]+)"').Groups[1].Value
if (!$appVersion) { throw 'Versão não encontrada em src\Updater.cs.' }
& $iscc "/DSourceDir=$OutputDirectory" "/DAppVersion=$appVersion" "/DOutputDir=$OutputDirectory" "/DOutputBaseFilename=MyShot-Setup" "$root\installer\MyShot.iss"
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o instalador.' }
Get-Item "$OutputDirectory\MyShot.exe" | Select-Object FullName,Length
