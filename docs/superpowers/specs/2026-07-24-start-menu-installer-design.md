# NoteStitch Start Menu Installer Design

## Goal

Installing NoteStitch with `winget install BDvirus.NoteStitch` must create a
searchable Start menu entry before the WinGet command completes. Installation
must be per-user and must not require administrator permission.

## Distribution approach

NoteStitch will use an Inno Setup executable installer for WinGet distribution.
The existing ZIP may remain available as an optional portable download, but it
will no longer be the installer referenced by the WinGet manifest.

MSIX is not used because it would add signing and packaging complexity to the
current unpackaged WinUI application. A custom bootstrapper is not used because
maintaining installer and uninstaller behavior would duplicate established
Inno Setup functionality.

## Installer behavior

The installer will:

- Install the published application under
  `%LOCALAPPDATA%\Programs\NoteStitch`.
- Create a per-user Start menu shortcut named `NoteStitch`.
- Register a per-user uninstaller.
- Require no elevation or administrator prompt.
- Support silent installation and upgrade through WinGet.
- Replace application files during an upgrade while retaining user settings
  stored under `%AppData%\NoteStitch`.
- Use the existing NoteStitch icon for the installed application and shortcut.

The Start menu shortcut must be created by the installer itself. Application
startup is not part of installation, and first-run shortcut creation is not
relied upon.

## Release workflow

The release workflow will:

1. Publish the self-contained WinUI application directory.
2. Build `NoteStitch-Setup.exe` from that directory with Inno Setup.
3. Attach the setup executable to the versioned GitHub Release.
4. Keep `NoteStitch.zip` as an optional portable release artifact if desired.
5. Trigger the existing WinGet submission workflow using
   `NoteStitch-Setup.exe`.

The WinGet seed manifest will describe the installer as Inno Setup rather than
as a ZIP containing a portable executable. Each version submission will use the
new setup asset URL and its computed SHA-256 hash.

## Upgrade and uninstall

All versions will use the same Inno Setup application identifier and install
directory so an update is recognized as an upgrade. The installer will close
or prompt to close a running NoteStitch instance when files must be replaced.

Uninstall will remove installed application files and Start menu shortcuts.
It will preserve `%AppData%\NoteStitch` settings to avoid deleting user data
without explicit consent.

## Verification

Automated repository checks will verify:

- The installer is configured for per-user, non-administrative installation.
- The Start menu shortcut points to the installed `NoteStitch.exe`.
- Silent install and uninstall switches are supported.
- The release workflow attaches `NoteStitch-Setup.exe`.
- The WinGet workflow selects the setup executable rather than the ZIP.
- The WinGet manifest no longer declares a portable nested installer.

Release verification should also install the setup executable in Windows
Sandbox or a clean Windows test account and confirm that:

1. No elevation prompt appears.
2. NoteStitch is searchable in the Start menu immediately after installation.
3. The shortcut starts the application.
4. Upgrade preserves settings.
5. Uninstall removes the shortcut and application files.
