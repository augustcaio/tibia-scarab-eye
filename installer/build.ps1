# Gera o instalador: publica o programa (autocontido, nao precisa do .NET instalado) e compila o instalador com o Inno Setup.
# Uso: pwsh installer\build.ps1 [-Version 0.2.0]
# Saida: installer\Output\TibiaScarabEye-Setup-<versao>.exe
param([string]$Version = "0.2.0")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

Remove-Item "artifacts\publish" -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish src\TibiaScarabEye\TibiaScarabEye.csproj -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o artifacts\publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou" }

# O compilador do Inno Setup vem do pacote NuGet Tools.InnoSetup, na versao fixada em Directory.Packages.props.
dotnet restore installer\InstallerTools.csproj
if ($LASTEXITCODE -ne 0) { throw "dotnet restore falhou" }
[xml]$props = Get-Content Directory.Packages.props
$inno = ($props.Project.ItemGroup.PackageVersion | Where-Object { $_.Include -eq "Tools.InnoSetup" }).Version
$packages = ((dotnet nuget locals global-packages --list) -replace "^.*?: ", "").Trim()
$iscc = Join-Path $packages "tools.innosetup\$inno\tools\ISCC.exe"
if (-not (Test-Path $iscc)) { throw "ISCC.exe nao encontrado em $iscc" }

& $iscc "/DAppVersion=$Version" installer\TibiaScarabEye.iss
if ($LASTEXITCODE -ne 0) { throw "ISCC falhou" }
Write-Host "Instalador: installer\Output\TibiaScarabEye-Setup-$Version.exe"
