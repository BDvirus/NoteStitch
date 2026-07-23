$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$workflowPath = Join-Path $root '.github\workflows\build.yml'
$workflow = Get-Content -Raw -LiteralPath $workflowPath

$required = @(
    'choco install innosetup --version=6.7.3 --no-progress -y',
    'installer\NoteStitch.iss',
    'Copy-Item artifacts/installer/NoteStitch-Setup.exe NoteStitch-Setup.exe -Force',
    'dist/NoteStitch-Setup.exe'
)

foreach ($entry in $required) {
    if (-not $workflow.Contains($entry)) {
        throw "Release workflow is missing: $entry"
    }
}

if ($workflow -notmatch '(?m)^permissions:\s*\r?\n\s+contents:\s+read\s*$') {
    throw 'Release workflow must default to contents: read permissions.'
}

if ($workflow -notmatch '(?ms)^  release:.*?^    permissions:\s*\r?\n\s+contents:\s+write\s*$') {
    throw 'Only the release job may receive contents: write permissions.'
}

$uploadContract = '(?ms)- name: Upload artifact.*?path:\s*\|\s*\r?\n\s*NoteStitch\.zip\s*\r?\n\s*NoteStitch-Setup\.exe'
if ($workflow -notmatch $uploadContract) {
    throw 'Release workflow must upload NoteStitch.zip and NoteStitch-Setup.exe from the artifact root.'
}

Write-Host 'PASS: release workflow builds and publishes the setup executable.'
