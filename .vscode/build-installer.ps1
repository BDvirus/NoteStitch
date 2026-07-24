$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $root 'NoteStitch\NoteStitch.csproj'
$profilePath = Join-Path $root 'NoteStitch\Properties\PublishProfiles\Installer.pubxml'
$publishDir = Join-Path $root 'artifacts\publish'
$installerDefinition = Join-Path $root 'installer\NoteStitch.iss'
$installerPath = Join-Path $root 'artifacts\installer\NoteStitch-Setup.exe'

function Find-InnoCompiler {
    $candidates = @()

    if (${env:ProgramFiles(x86)}) {
        $candidates += Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    }

    if ($env:ProgramFiles) {
        $candidates += Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'
    }

    if ($env:LOCALAPPDATA) {
        $candidates += Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
    }

    return $candidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

$iscc = Find-InnoCompiler
if (-not $iscc) {
    $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw 'Inno Setup is missing and winget.exe is unavailable. Install App Installer, then run the task again.'
    }

    Write-Host 'Inno Setup 6 was not found. Installing it with WinGet...'
    & $winget.Source install `
        --id JRSoftware.InnoSetup `
        --exact `
        --silent `
        --accept-package-agreements `
        --accept-source-agreements

    if ($LASTEXITCODE -ne 0) {
        throw "WinGet failed to install Inno Setup (exit code $LASTEXITCODE)."
    }

    $iscc = Find-InnoCompiler
    if (-not $iscc) {
        throw 'WinGet completed, but ISCC.exe was not found in a standard Inno Setup 6 location.'
    }
}

Write-Host 'Publishing NoteStitch with Installer.pubxml...'
& dotnet publish $projectPath `
    "-p:PublishProfile=$profilePath"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed (exit code $LASTEXITCODE)."
}

$publishedExecutable = Join-Path $publishDir 'NoteStitch.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Published executable was not created: $publishedExecutable"
}

[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$versionNode = Select-Xml -Xml $project -XPath '/Project/PropertyGroup/Version' |
    Select-Object -First 1
$version = if ($versionNode) { $versionNode.Node.InnerText } else { $null }

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "No Version value was found in $projectPath."
}

Write-Host "Compiling NoteStitch installer v$version..."
& $iscc `
    "/DPublishDir=$publishDir" `
    "/DMyAppVersion=$version" `
    $installerDefinition

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed (exit code $LASTEXITCODE)."
}

if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Installer was not created: $installerPath"
}

Write-Host "Created installer: $installerPath" -ForegroundColor Green
