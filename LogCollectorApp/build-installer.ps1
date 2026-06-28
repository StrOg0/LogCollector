param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $Root "LogCollectorApp.csproj"
$PublishDir = Join-Path $Root ("publish\" + $Runtime)
$DistDir = Join-Path $Root "dist"
$InstallerScript = Join-Path $Root "Installer\LogCollectorApp.iss"

Write-Host "=== LogCollectorApp installer build ==="
Write-Host "Project: $ProjectFile"
Write-Host "Runtime: $Runtime"
Write-Host "Configuration: $Configuration"
Write-Host ""

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet was not found. Install the .NET SDK that matches the project TargetFramework. Current project expects net9.0-windows unless you changed it."
}

if (-not (Test-Path $ProjectFile)) {
    throw "Project file was not found: $ProjectFile"
}

if (-not (Test-Path $InstallerScript)) {
    throw "Inno Setup script was not found: $InstallerScript"
}

Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $DistDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

Write-Host "[1/3] Restoring NuGet packages..."
& dotnet restore $ProjectFile
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

Write-Host "[2/3] Publishing self-contained Windows build..."
$publishArgs = @(
    "publish", $ProjectFile,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-o", $PublishDir,
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:EnableCompressionInSingleFile=true",
    "/p:DebugType=None",
    "/p:DebugSymbols=false"
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$ExePath = Join-Path $PublishDir "LogCollectorApp.exe"
if (-not (Test-Path $ExePath)) {
    throw "Published exe was not found: $ExePath"
}

Write-Host "[3/3] Building installer with Inno Setup..."
$possibleIscc = @(
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $possibleIscc | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    $isccFromPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($isccFromPath) {
        $iscc = $isccFromPath.Source
    }
}

if (-not $iscc) {
    throw "ISCC.exe was not found. Install Inno Setup 6 or add ISCC.exe to PATH."
}

& $iscc $InstallerScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compiler failed" }

$Setup = Get-ChildItem $DistDir -Filter "LogCollectorApp_Setup_*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Setup) {
    throw "Installer was not found in folder: $DistDir"
}

Write-Host ""
Write-Host "Done. Installer created:"
Write-Host $Setup.FullName
