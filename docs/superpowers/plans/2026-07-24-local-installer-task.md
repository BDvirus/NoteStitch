# Local Installer Task Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a VS Code task that automatically ensures Inno Setup is installed and builds `artifacts/installer/NoteStitch-Setup.exe` from a checked-in XML publish profile.

**Architecture:** A static PowerShell contract test protects the packaging configuration. `Installer.pubxml` owns the deterministic .NET payload settings, `.vscode/build-installer.ps1` owns dependency discovery and orchestration, and `.vscode/tasks.json` exposes that script without duplicating packaging logic.

**Tech Stack:** .NET 10/MSBuild publish profiles, PowerShell 7/Windows PowerShell, VS Code tasks 2.0, WinGet, Inno Setup 6

## Global Constraints

- Preserve the existing per-user Inno Setup behavior in `installer/NoteStitch.iss`.
- Generate `artifacts/installer/NoteStitch-Setup.exe`.
- Publish a self-contained, untrimmed, ReadyToRun `win-x64` Release payload to `artifacts/publish`.
- Install `JRSoftware.InnoSetup` through WinGet only when `ISCC.exe` cannot be found.
- Resolve repository paths independently of the caller's current directory.
- Do not change the release version, WinGet manifests, or GitHub Actions flow.

---

### Task 1: Protect the local packaging contract

**Files:**
- Create: `tests/installer/Validate-LocalInstallerTask.ps1`

**Interfaces:**
- Consumes: repository-root-relative paths for the future publish profile, build script, VS Code task file, and existing Inno Setup definition
- Produces: a zero exit code and `PASS: local installer task configuration is valid.` when the complete local packaging contract is present

- [ ] **Step 1: Write the failing static validation test**

Create `tests/installer/Validate-LocalInstallerTask.ps1`:

```powershell
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
```

- [ ] **Step 2: Run the test and verify the expected failure**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-LocalInstallerTask.ps1
```

Expected: FAIL with `Required local installer file is missing` for
`Installer.pubxml`.

- [ ] **Step 3: Commit the red test**

```powershell
git add tests/installer/Validate-LocalInstallerTask.ps1
git commit -m "test: define local installer task contract"
```

### Task 2: Add the deterministic installer publish profile

**Files:**
- Create: `NoteStitch/Properties/PublishProfiles/Installer.pubxml`
- Test: `tests/installer/Validate-LocalInstallerTask.ps1`

**Interfaces:**
- Consumes: `NoteStitch/NoteStitch.csproj`
- Produces: a self-contained application payload under the repository's `artifacts/publish` directory

- [ ] **Step 1: Add the XML publish profile**

Create `NoteStitch/Properties/PublishProfiles/Installer.pubxml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishTrimmed>false</PublishTrimmed>
    <PublishReadyToRun>true</PublishReadyToRun>
    <PublishDir>$(MSBuildThisFileDirectory)..\..\..\artifacts\publish\</PublishDir>
    <DeleteExistingFiles>true</DeleteExistingFiles>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Run the contract test and verify it advances**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-LocalInstallerTask.ps1
```

Expected: FAIL for the next missing file,
`.vscode\build-installer.ps1`, proving that the publish-profile assertions now
pass.

- [ ] **Step 3: Verify the profile publishes the application**

Run:

```powershell
dotnet publish NoteStitch/NoteStitch.csproj `
  -p:PublishProfile=Installer `
  --no-restore
Test-Path artifacts/publish/NoteStitch.exe
```

Expected: publish succeeds and `Test-Path` prints `True`.

- [ ] **Step 4: Commit the publish profile**

```powershell
git add NoteStitch/Properties/PublishProfiles/Installer.pubxml
git commit -m "build: add installer publish profile"
```

### Task 3: Add the reusable installer build script and VS Code task

**Files:**
- Create: `.vscode/build-installer.ps1`
- Modify: `.vscode/tasks.json`
- Test: `tests/installer/Validate-LocalInstallerTask.ps1`

**Interfaces:**
- Consumes: `NoteStitch/NoteStitch.csproj`, `Installer.pubxml`, `installer/NoteStitch.iss`, WinGet when Inno Setup is missing
- Produces: `artifacts/installer/NoteStitch-Setup.exe` and a successful VS Code task exit code

- [ ] **Step 1: Add the orchestration script**

Create `.vscode/build-installer.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $root 'NoteStitch\NoteStitch.csproj'
$profilePath = Join-Path $root 'NoteStitch\Properties\PublishProfiles\Installer.pubxml'
$publishDir = Join-Path $root 'artifacts\publish'
$installerDefinition = Join-Path $root 'installer\NoteStitch.iss'
$installerPath = Join-Path $root 'artifacts\installer\NoteStitch-Setup.exe'

function Find-InnoCompiler {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

    return $candidates | Select-Object -First 1
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
$version = @($project.Project.PropertyGroup.Version) |
    Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
    Select-Object -First 1

if (-not $version) {
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
```

- [ ] **Step 2: Add the VS Code task**

Append this object to the `tasks` array in `.vscode/tasks.json`:

```json
{
  "label": "Installer: Build NoteStitch-Setup.exe",
  "type": "shell",
  "command": "powershell",
  "options": {
    "cwd": "${workspaceFolder}"
  },
  "args": [
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    "${workspaceFolder}\\.vscode\\build-installer.ps1"
  ],
  "presentation": {
    "echo": true,
    "reveal": "always",
    "panel": "shared",
    "clear": true
  },
  "problemMatcher": []
}
```

- [ ] **Step 3: Run the contract test and verify it passes**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-LocalInstallerTask.ps1
```

Expected:
`PASS: local installer task configuration is valid.`

- [ ] **Step 4: Run all installer configuration tests**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-Installer.ps1
pwsh -NoProfile -File tests/installer/Validate-PublishAssets.ps1 `
  -PublishDirectory artifacts/publish
pwsh -NoProfile -File tests/installer/Validate-ReleaseWorkflow.ps1
pwsh -NoProfile -File tests/installer/Validate-WinGet.ps1
```

Expected: every script prints `PASS`.

- [ ] **Step 5: Commit the script and task**

```powershell
git add .vscode/build-installer.ps1 .vscode/tasks.json
git commit -m "build: add local installer VS Code task"
```

### Task 4: Verify the end-to-end installer build

**Files:**
- Verify: `.vscode/build-installer.ps1`
- Verify: `artifacts/installer/NoteStitch-Setup.exe`

**Interfaces:**
- Consumes: all components from Tasks 1–3
- Produces: verified local installer executable and passing application regression suite

- [ ] **Step 1: Run the exact command used by VS Code**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .vscode/build-installer.ps1
```

Expected: Inno Setup is discovered or installed automatically, publishing and
compilation succeed, and the final line starts with `Created installer:`.

- [ ] **Step 2: Verify the generated executable**

Run:

```powershell
$installer = Get-Item artifacts/installer/NoteStitch-Setup.exe
if ($installer.Length -le 0) {
    throw 'Generated installer is empty.'
}
$installer.FullName
```

Expected: prints the absolute path to a nonempty
`NoteStitch-Setup.exe`.

- [ ] **Step 3: Run application and repository verification**

Run:

```powershell
dotnet run --project NoteStitch.Tests/NoteStitch.Tests.csproj --no-restore
dotnet build NoteStitch/NoteStitch.csproj -c Debug --no-restore
git diff --check
git status --short
```

Expected: regression tests pass, build succeeds with zero warnings and zero
errors, `git diff --check` is silent, and status lists only the intended local
installer files plus the user's pre-existing changes.

- [ ] **Step 4: Commit any verification-only test adjustments**

If end-to-end verification required a correction to
`tests/installer/Validate-LocalInstallerTask.ps1`, commit only that correction:

```powershell
git add tests/installer/Validate-LocalInstallerTask.ps1
git commit -m "test: finalize local installer validation"
```

If no correction was needed, do not create an empty commit.
