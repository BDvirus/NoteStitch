$ErrorActionPreference = 'Stop'
$workflowPath = Join-Path $PSScriptRoot '..\..\.github\workflows\build.yml'
$workflow = Get-Content -Raw -LiteralPath $workflowPath

$required = @(
    'choco install innosetup --no-progress -y',
    'installer\NoteStitch.iss',
    'artifacts/installer/NoteStitch-Setup.exe',
    'dist/NoteStitch-Setup.exe'
)

foreach ($entry in $required) {
    if (-not $workflow.Contains($entry)) {
        throw "Release workflow is missing: $entry"
    }
}

Write-Host 'PASS: release workflow builds and publishes the setup executable.'
