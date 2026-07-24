# Local Installer Task Design

## Goal

Provide a VS Code task that creates
`artifacts/installer/NoteStitch-Setup.exe` on Windows from a reproducible .NET
publish profile. If Inno Setup is unavailable, the task installs it
automatically with WinGet before compiling the existing installer definition.

## Architecture

The local packaging flow has three focused components:

1. `NoteStitch/Properties/PublishProfiles/Installer.pubxml` defines the
   application payload: Release configuration, `win-x64`, self-contained,
   untrimmed, and published to `artifacts/publish`.
2. `.vscode/build-installer.ps1` orchestrates publishing and installer
   compilation. It locates Inno Setup, installs `JRSoftware.InnoSetup` with
   WinGet when necessary, reads the application version from
   `NoteStitch.csproj`, and invokes `installer/NoteStitch.iss`.
3. `.vscode/tasks.json` exposes the script as
   `Installer: Build NoteStitch-Setup.exe`.

The existing Inno Setup definition remains the single source of truth for
installation behavior, including per-user installation and Start menu shortcut
creation.

## Publish Profile

`Installer.pubxml` will set:

- `Configuration` to `Release`
- `TargetFramework` to `net10.0-windows10.0.19041.0`
- `RuntimeIdentifier` to `win-x64`
- `SelfContained` to `true`
- `PublishTrimmed` to `false`
- `PublishReadyToRun` to `true`
- `PublishDir` to `artifacts/publish` relative to the repository
- `DeleteExistingFiles` to `true`

The script will invoke the profile by name so both VS Code and direct
PowerShell usage produce the same payload.

## Inno Setup Bootstrap

The build script will check standard per-machine and per-user Inno Setup 6
locations before installing anything. If `ISCC.exe` is not found, it will:

1. Confirm that `winget.exe` is available.
2. Run `winget install --id JRSoftware.InnoSetup --exact --silent
   --accept-package-agreements --accept-source-agreements`.
3. Search the known installation locations again.
4. Stop with a clear error if installation failed or `ISCC.exe` is still
   unavailable.

The installation is attempted only when required. A nonzero WinGet or Inno
Setup exit code fails the VS Code task.

## Packaging Flow

The script will:

1. Resolve all paths from the repository root rather than the caller's current
   directory.
2. Publish `NoteStitch.csproj` with `Installer.pubxml`.
3. Verify that the expected publish directory and `NoteStitch.exe` exist.
4. read the first nonempty project `Version` value.
5. invoke `ISCC.exe` with absolute `PublishDir` and `MyAppVersion`
   preprocessor definitions.
6. verify that `artifacts/installer/NoteStitch-Setup.exe` exists.
7. print the absolute installer path.

## Error Handling

The script uses terminating errors. Missing WinGet, failed dependency
installation, failed publishing, missing publish output, an absent project
version, failed Inno compilation, or a missing final executable each produce a
specific error and a failing task exit code.

The task will not silently reuse an incomplete payload or report success unless
the final installer exists.

## Testing

Static validation will confirm that:

- the publish profile contains the required packaging properties;
- the VS Code task calls the orchestration script;
- the script contains the WinGet bootstrap contract;
- the script passes `PublishDir` and `MyAppVersion` to the existing Inno Setup
  definition.

End-to-end verification will run the VS Code-equivalent PowerShell script,
confirm `NoteStitch-Setup.exe` is generated, and retain the existing installer,
publish-asset, application regression, and build checks.

## Scope

This change adds a local installer-build entry point only. It does not alter the
installer's installation directory, privilege level, Start menu behavior,
release version, WinGet manifest, or GitHub Actions release flow.
