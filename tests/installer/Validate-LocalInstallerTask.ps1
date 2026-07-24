$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$profilePath = Join-Path $root 'NoteStitch\Properties\PublishProfiles\Installer.pubxml'
$scriptPath = Join-Path $root '.vscode\build-installer.ps1'
$tasksPath = Join-Path $root '.vscode\tasks.json'

foreach ($path in @($profilePath, $scriptPath, $tasksPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required local installer file is missing: $path"
    }
}

[xml]$profile = Get-Content -LiteralPath $profilePath -Raw
$properties = $profile.Project.PropertyGroup
$expectedProperties = @{
    Configuration = 'Release'
    TargetFramework = 'net10.0-windows10.0.19041.0'
    RuntimeIdentifier = 'win-x64'
    SelfContained = 'true'
    PublishTrimmed = 'false'
    PublishReadyToRun = 'true'
    DeleteExistingFiles = 'true'
}

foreach ($entry in $expectedProperties.GetEnumerator()) {
    if ([string]$properties.($entry.Key) -ne $entry.Value) {
        throw "Installer.pubxml must set $($entry.Key) to $($entry.Value)."
    }
}

if ([string]$properties.PublishDir -notmatch 'artifacts[\\/]publish') {
    throw 'Installer.pubxml must publish to artifacts/publish.'
}

$script = Get-Content -LiteralPath $scriptPath -Raw
$requiredScriptEntries = @(
    'JRSoftware.InnoSetup',
    '--accept-package-agreements',
    '--accept-source-agreements',
    'Installer.pubxml',
    "Select-Xml -Xml `$project -XPath '/Project/PropertyGroup/Version'",
    '/DPublishDir=',
    '/DMyAppVersion=',
    'NoteStitch-Setup.exe'
)

foreach ($entry in $requiredScriptEntries) {
    if (-not $script.Contains($entry)) {
        throw "build-installer.ps1 is missing required entry: $entry"
    }
}

$tasks = Get-Content -LiteralPath $tasksPath -Raw | ConvertFrom-Json
$installerTask = @($tasks.tasks) |
    Where-Object { $_.label -eq 'Installer: Build NoteStitch-Setup.exe' } |
    Select-Object -First 1

if ($null -eq $installerTask) {
    throw 'VS Code installer task is missing.'
}

if (-not (@($installerTask.args) -contains '${workspaceFolder}\.vscode\build-installer.ps1')) {
    throw 'VS Code installer task must call build-installer.ps1.'
}

Write-Host 'PASS: local installer task configuration is valid.'
