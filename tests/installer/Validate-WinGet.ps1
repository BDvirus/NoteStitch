$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$workflow = Get-Content -Raw -LiteralPath (Join-Path $root '.github\workflows\winget.yml')
$readme = Get-Content -Raw -LiteralPath (Join-Path $root 'README.md')
$installer = Get-Content -Raw -LiteralPath (Join-Path $root 'winget\BDvirus.NoteStitch.installer.yaml')
$versionManifest = Get-Content -Raw -LiteralPath (Join-Path $root 'winget\BDvirus.NoteStitch.yaml')
$localeManifest = Get-Content -Raw -LiteralPath (Join-Path $root 'winget\BDvirus.NoteStitch.locale.en-US.yaml')
$project = Get-Content -Raw -LiteralPath (Join-Path $root 'NoteStitch\NoteStitch.csproj')

$requiredWorkflowEntries = @(
    'workflow_run:',
    'workflows: ["Build & Release"]',
    "types: [completed]",
    "github.event.workflow_run.conclusion == 'success'",
    "startsWith(github.event.workflow_run.head_branch, 'v')",
    'uses: actions/checkout@v5',
    'ref: main',
    'NoteStitch-Setup.exe',
    'Get-FileHash',
    "NestedInstaller(Type|Files)|ArchiveBinariesDependOnPath",
    'Verify WinGet token access',
    'https://api.github.com/user',
    'https://api.github.com/repos/BDvirus/winget-pkgs',
    "permissions.push",
    "x-oauth-scopes",
    'cargo binstall komac -y',
    'komac submit winget --yes',
    'GITHUB_TOKEN: ${{ secrets.WINGET_TOKEN }}'
)

foreach ($entry in $requiredWorkflowEntries) {
    if (-not $workflow.Contains($entry)) {
        throw "WinGet workflow is missing the release-completion contract: $entry"
    }
}

if ($workflow -match '(?m)^\s*release:\s*$') {
    throw 'WinGet workflow must not depend on a release event created by GITHUB_TOKEN.'
}

if ($workflow.Contains('vedantmgoyal9/winget-releaser')) {
    throw 'WinGet workflow must submit the corrected manifests instead of inheriting portable fields.'
}

if (-not $workflow.Contains('the build workflow attaches NoteStitch.zip and NoteStitch-Setup.exe')) {
    throw 'WinGet workflow must state that releases attach both the ZIP and setup executable.'
}

if (-not $workflow.Contains('selects NoteStitch-Setup.exe')) {
    throw 'WinGet workflow must state that WinGet selects the setup executable.'
}

$migrationGuidance = @(
    'Existing WinGet 1.0.13 users',
    'winget uninstall BDvirus.NoteStitch',
    'winget install BDvirus.NoteStitch',
    '%AppData%\NoteStitch',
    'preserved'
)

foreach ($entry in $migrationGuidance) {
    if (-not $readme.Contains($entry)) {
        throw "README migration guidance is missing: $entry"
    }
}

$requiredInstallerEntries = @(
    'PackageVersion: 1.0.14',
    'InstallerType: inno',
    'Scope: user',
    'InstallerUrl: https://github.com/BDvirus/NoteStitch/releases/download/v1.0.14/NoteStitch-Setup.exe'
)

foreach ($entry in $requiredInstallerEntries) {
    if (-not $installer.Contains($entry)) {
        throw "WinGet installer manifest is missing: $entry"
    }
}

if ($installer -match 'NestedInstaller(Type|Files)|InstallerType:\s*zip') {
    throw 'WinGet manifest must not declare a portable ZIP installer.'
}

foreach ($manifest in @($versionManifest, $localeManifest)) {
    if (-not $manifest.Contains('PackageVersion: 1.0.14')) {
        throw 'Every WinGet manifest must use version 1.0.14.'
    }
}

if (-not $project.Contains('<Version>1.0.14</Version>')) {
    throw 'The application project version must be 1.0.14.'
}

Write-Host 'PASS: WinGet installs the per-user setup executable.'
