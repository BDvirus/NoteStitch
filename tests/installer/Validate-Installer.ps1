$ErrorActionPreference = 'Stop'
$installerPath = Join-Path $PSScriptRoot '..\..\installer\NoteStitch.iss'

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Missing installer definition: $installerPath"
}

$installer = Get-Content -Raw -LiteralPath $installerPath
$required = @(
    'AppId=BDvirus.NoteStitch',
    'PrivilegesRequired=lowest',
    'DefaultDirName={localappdata}\Programs\NoteStitch',
    'OutputBaseFilename=NoteStitch-Setup',
    'UninstallDisplayIcon={app}\NoteStitch.exe',
    'CloseApplications=yes',
    'Name: "{userprograms}\NoteStitch"; Filename: "{app}\NoteStitch.exe"'
)

foreach ($entry in $required) {
    if (-not $installer.Contains($entry)) {
        throw "Installer is missing required policy: $entry"
    }
}

if ($installer -match 'PrivilegesRequired=(admin|poweruser)') {
    throw 'Installer must not request administrative privileges.'
}

if ($installer -match '\{appdata\}\\NoteStitch') {
    throw 'The installer must not remove or overwrite the user settings directory.'
}

Write-Host 'PASS: installer is per-user and creates a Start menu shortcut.'
