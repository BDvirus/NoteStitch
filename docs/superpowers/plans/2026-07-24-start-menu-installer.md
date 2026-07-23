# NoteStitch Start Menu Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a non-elevated per-user Inno Setup installer that creates a searchable NoteStitch Start menu shortcut before `winget install` completes.

**Architecture:** Keep the existing self-contained WinUI publish directory as the application payload, then wrap it in a conventional Inno Setup installer. The release workflow publishes both the new setup executable and the optional portable ZIP, while the WinGet workflow and seed manifest select only the setup executable.

**Tech Stack:** .NET 10, WinUI 3, Inno Setup 6, PowerShell, GitHub Actions, WinGet manifests

## Global Constraints

- Installation is per-user under `%LOCALAPPDATA%\Programs\NoteStitch`.
- Installation must not request elevation.
- The installer creates the `NoteStitch` Start menu shortcut before returning success.
- User settings under `%AppData%\NoteStitch` survive upgrades and uninstall.
- The ZIP remains an optional portable artifact and is not referenced by WinGet.
- Release version is `1.0.14`.

---

### Task 1: Per-user Inno Setup package

**Files:**
- Create: `installer/NoteStitch.iss`
- Create: `tests/installer/Validate-Installer.ps1`

**Interfaces:**
- Consumes: the published application directory passed as the Inno preprocessor symbol `PublishDir`, and version passed as `MyAppVersion`
- Produces: `artifacts/installer/NoteStitch-Setup.exe`

- [ ] **Step 1: Write the failing installer-policy test**

Create `tests/installer/Validate-Installer.ps1`:

```powershell
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
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-Installer.ps1
```

Expected: FAIL with `Missing installer definition`.

- [ ] **Step 3: Add the minimal Inno Setup definition**

Create `installer/NoteStitch.iss`:

```iss
#ifndef PublishDir
  #error PublishDir must point to the dotnet publish directory
#endif

#ifndef MyAppVersion
  #error MyAppVersion must be supplied by the build
#endif

[Setup]
AppId=BDvirus.NoteStitch
AppName=NoteStitch
AppVersion={#MyAppVersion}
AppPublisher=BDvirus
AppPublisherURL=https://github.com/BDvirus/NoteStitch
AppSupportURL=https://github.com/BDvirus/NoteStitch/issues
DefaultDirName={localappdata}\Programs\NoteStitch
DefaultGroupName=NoteStitch
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=NoteStitch-Setup
SetupIconFile=..\NoteStitch\Assets\icon.ico
UninstallDisplayIcon={app}\NoteStitch.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=NoteStitch.exe
RestartApplications=no

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\NoteStitch"; Filename: "{app}\NoteStitch.exe"; WorkingDir: "{app}"; IconFilename: "{app}\NoteStitch.exe"
```

- [ ] **Step 4: Run the policy test and compile the installer**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-Installer.ps1
dotnet publish NoteStitch/NoteStitch.csproj -c Release -o publish
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" `
  "/DPublishDir=$((Resolve-Path publish).Path)" `
  "/DMyAppVersion=1.0.14" `
  "installer\NoteStitch.iss"
```

Expected: policy test prints `PASS`; Inno Setup creates `artifacts/installer/NoteStitch-Setup.exe`.

- [ ] **Step 5: Commit**

```powershell
git add installer/NoteStitch.iss tests/installer/Validate-Installer.ps1
git commit -m "feat: add per-user NoteStitch installer"
```

### Task 2: Release the installer artifact

**Files:**
- Create: `tests/installer/Validate-ReleaseWorkflow.ps1`
- Modify: `.github/workflows/build.yml`

**Interfaces:**
- Consumes: `installer/NoteStitch.iss` from Task 1 and the `publish` directory produced by `dotnet publish`
- Produces: the `NoteStitch-Setup.exe` build artifact and GitHub Release asset

- [ ] **Step 1: Write the failing workflow test**

Create `tests/installer/Validate-ReleaseWorkflow.ps1`:

```powershell
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
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-ReleaseWorkflow.ps1
```

Expected: FAIL with `Release workflow is missing: choco install innosetup --no-progress -y`.

- [ ] **Step 3: Build the installer in the workflow**

In `.github/workflows/build.yml`, insert after the `Publish` step:

```yaml
      - name: Install Inno Setup
        run: choco install innosetup --no-progress -y

      - name: Build installer
        shell: pwsh
        run: |
          [xml]$project = Get-Content NoteStitch/NoteStitch.csproj
          $version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
          $publishDir = (Resolve-Path publish).Path
          & "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" `
            "/DPublishDir=$publishDir" `
            "/DMyAppVersion=$version" `
            "installer\NoteStitch.iss"
```

Change the upload step to:

```yaml
      - name: Upload artifact
        uses: actions/upload-artifact@v5
        with:
          name: NoteStitch
          path: |
            NoteStitch.zip
            artifacts/installer/NoteStitch-Setup.exe
```

Change the `gh release create` asset arguments to:

```yaml
          gh release create "${{ github.ref_name }}" \
            dist/NoteStitch.zip \
            dist/NoteStitch-Setup.exe \
            --repo "${{ github.repository }}" \
            --title "${{ github.ref_name }}" \
            --generate-notes
```

- [ ] **Step 4: Run the workflow test**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-ReleaseWorkflow.ps1
```

Expected: `PASS: release workflow builds and publishes the setup executable.`

- [ ] **Step 5: Commit**

```powershell
git add .github/workflows/build.yml tests/installer/Validate-ReleaseWorkflow.ps1
git commit -m "ci: publish NoteStitch setup executable"
```

### Task 3: Route WinGet releases to the setup executable

**Files:**
- Create: `tests/installer/Validate-WinGet.ps1`
- Modify: `.github/workflows/winget.yml`
- Modify: `winget/BDvirus.NoteStitch.installer.yaml`
- Modify: `winget/BDvirus.NoteStitch.yaml`
- Modify: `winget/BDvirus.NoteStitch.locale.en-US.yaml`
- Modify: `NoteStitch/NoteStitch.csproj`

**Interfaces:**
- Consumes: the GitHub Release asset `NoteStitch-Setup.exe` from Task 2
- Produces: a WinGet update submission for `BDvirus.NoteStitch` version `1.0.14`

- [ ] **Step 1: Write the failing WinGet routing test**

Create `tests/installer/Validate-WinGet.ps1`:

```powershell
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
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-WinGet.ps1
```

Expected: FAIL with `WinGet workflow must select NoteStitch-Setup.exe`.

- [ ] **Step 3: Update the WinGet workflow and version**

In `.github/workflows/winget.yml`, change:

```yaml
          installers-regex: 'NoteStitch-Setup\.exe$'
```

In `NoteStitch/NoteStitch.csproj`, change:

```xml
    <Version>1.0.14</Version>
```

Set `PackageVersion: 1.0.14` in all three files under `winget/`.

- [ ] **Step 4: Replace the portable seed manifest**

Replace the installer-specific portion of
`winget/BDvirus.NoteStitch.installer.yaml` with:

```yaml
PackageIdentifier: BDvirus.NoteStitch
PackageVersion: 1.0.14
InstallerType: inno
Scope: user
UpgradeBehavior: install
InstallModes:
  - interactive
  - silent
  - silentWithProgress
Installers:
  - Architecture: x64
    InstallerUrl: https://github.com/BDvirus/NoteStitch/releases/download/v1.0.14/NoteStitch-Setup.exe
    InstallerSha256: 0000000000000000000000000000000000000000000000000000000000000000
ManifestType: installer
ManifestVersion: 1.6.0
```

Keep the existing schema header and source-of-truth comment. The zero hash is
only a repository seed; `winget-releaser` computes the real release hash before
opening the external PR.

- [ ] **Step 5: Run all repository checks and build**

Run:

```powershell
pwsh -NoProfile -File tests/installer/Validate-Installer.ps1
pwsh -NoProfile -File tests/installer/Validate-ReleaseWorkflow.ps1
pwsh -NoProfile -File tests/installer/Validate-WinGet.ps1
dotnet run --project NoteStitch.Tests/NoteStitch.Tests.csproj --no-restore
dotnet build NoteStitch/NoteStitch.csproj --no-restore -c Debug
git diff --check
```

Expected:

- All three PowerShell checks print `PASS`.
- Both NoteStitch regression checks print `PASS`.
- Build succeeds with 0 warnings and 0 errors.
- `git diff --check` exits successfully.

- [ ] **Step 6: Commit**

```powershell
git add .github/workflows/winget.yml winget NoteStitch/NoteStitch.csproj tests/installer/Validate-WinGet.ps1
git commit -m "release: prepare WinGet installer version 1.0.14"
```

### Task 4: Clean-Windows acceptance test

**Files:**
- Test: `artifacts/installer/NoteStitch-Setup.exe`

**Interfaces:**
- Consumes: the compiled setup executable from Task 1
- Produces: manual evidence that Windows creates, resolves, and removes the Start menu shortcut correctly

- [ ] **Step 1: Install silently as the current user**

In Windows Sandbox or a clean Windows test account, run:

```powershell
.\NoteStitch-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

Expected: exit code `0`, no UAC prompt, and no installer UI.

- [ ] **Step 2: Verify the installed files and shortcut**

Run:

```powershell
$exe = Join-Path $env:LOCALAPPDATA 'Programs\NoteStitch\NoteStitch.exe'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\NoteStitch.lnk'
if (-not (Test-Path -LiteralPath $exe)) { throw "Missing installed executable: $exe" }
if (-not (Test-Path -LiteralPath $shortcut)) { throw "Missing Start menu shortcut: $shortcut" }
Start-Process -FilePath $shortcut
```

Expected: both paths exist and NoteStitch starts from the shortcut.

- [ ] **Step 3: Verify settings survive an upgrade**

Create `%AppData%\NoteStitch\settings.json`, run the installer a second time
with the same silent arguments, then run:

```powershell
$settings = Join-Path $env:APPDATA 'NoteStitch\settings.json'
if (-not (Test-Path -LiteralPath $settings)) { throw 'Upgrade removed user settings.' }
```

Expected: the settings file still exists.

- [ ] **Step 4: Verify uninstall removes application registration**

Run:

```powershell
$uninstaller = Join-Path $env:LOCALAPPDATA 'Programs\NoteStitch\unins000.exe'
& $uninstaller /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
$exe = Join-Path $env:LOCALAPPDATA 'Programs\NoteStitch\NoteStitch.exe'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\NoteStitch.lnk'
if (Test-Path -LiteralPath $exe) { throw 'Uninstall left the application executable.' }
if (Test-Path -LiteralPath $shortcut) { throw 'Uninstall left the Start menu shortcut.' }
```

Expected: the installed executable and Start menu shortcut are removed while
`%AppData%\NoteStitch` remains intact.

- [ ] **Step 5: Record acceptance result in the release notes**

Add this checklist to the `v1.0.14` GitHub Release description:

```markdown
- [x] Per-user install completed without UAC
- [x] Start menu entry appeared immediately
- [x] Start menu shortcut launched NoteStitch
- [x] Upgrade preserved settings
- [x] Uninstall removed app files and shortcut
```
