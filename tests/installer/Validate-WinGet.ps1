$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$workflow = Get-Content -Raw -LiteralPath (Join-Path $root '.github\workflows\winget.yml')
$installer = Get-Content -Raw -LiteralPath (Join-Path $root 'winget\BDvirus.NoteStitch.installer.yaml')
$versionManifest = Get-Content -Raw -LiteralPath (Join-Path $root 'winget\BDvirus.NoteStitch.yaml')
$localeManifest = Get-Content -Raw -LiteralPath (Join-Path $root 'winget\BDvirus.NoteStitch.locale.en-US.yaml')
$project = Get-Content -Raw -LiteralPath (Join-Path $root 'NoteStitch\NoteStitch.csproj')

if (-not $workflow.Contains("installers-regex: 'NoteStitch-Setup\.exe$'")) {
    throw 'WinGet workflow must select NoteStitch-Setup.exe.'
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
