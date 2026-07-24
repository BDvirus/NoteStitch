# Title-Bar Logo Size Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Increase only the custom title-bar NoteStitch logo from 24×24 pixels to 40×40 pixels.

**Architecture:** A focused PowerShell layout test parses `MainWindow.xaml` and identifies the image inside `AppIconBadge` by its asset source. The production change modifies only that image's width and height, preserving the existing title-bar container and surrounding layout.

**Tech Stack:** WinUI 3 XAML, PowerShell XML validation, .NET 10

## Global Constraints

- The title-bar logo width and height must both equal 40 pixels.
- The `AppIconBadge` must remain 40×40 pixels.
- The title-bar height, padding, text layout, caption buttons, image assets, About page, application icon, and installer icon must remain unchanged.
- Do not regenerate or edit image assets.

---

### Task 1: Enlarge the title-bar logo

**Files:**
- Create: `tests/ui/Validate-TitleBarLayout.ps1`
- Modify: `NoteStitch/MainWindow.xaml:24-28`

**Interfaces:**
- Consumes: the `AppIconBadge` element and its nested image sourced from `ms-appx:///Assets/notes.png`
- Produces: a title-bar image with `Width="40"` and `Height="40"`, plus a repeatable layout regression check

- [ ] **Step 1: Write the failing layout test**

Create `tests/ui/Validate-TitleBarLayout.ps1`:

```powershell
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$xamlPath = Join-Path $root 'NoteStitch\MainWindow.xaml'
[xml]$xaml = Get-Content -LiteralPath $xamlPath -Raw

$namespace = New-Object System.Xml.XmlNamespaceManager($xaml.NameTable)
$namespace.AddNamespace('ui', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$namespace.AddNamespace('xaml', 'http://schemas.microsoft.com/winfx/2006/xaml')

$badge = $xaml.SelectSingleNode(
    "//ui:Border[@xaml:Name='AppIconBadge']",
    $namespace
)

if ($null -eq $badge) {
    throw 'AppIconBadge was not found in MainWindow.xaml.'
}

if ($badge.Width -ne '40' -or $badge.Height -ne '40') {
    throw 'AppIconBadge must remain 40x40 pixels.'
}

$image = $badge.SelectSingleNode(
    ".//ui:Image[@Source='ms-appx:///Assets/notes.png']",
    $namespace
)

if ($null -eq $image) {
    throw 'The title-bar notes.png image was not found inside AppIconBadge.'
}

if ($image.Width -ne '40' -or $image.Height -ne '40') {
    throw "The title-bar logo must be 40x40 pixels; found $($image.Width)x$($image.Height)."
}

Write-Host 'PASS: title-bar logo is 40x40 pixels.'
```

- [ ] **Step 2: Run the test and verify it fails for the current dimensions**

Run:

```powershell
pwsh -NoProfile -File tests/ui/Validate-TitleBarLayout.ps1
```

Expected: FAIL with
`The title-bar logo must be 40x40 pixels; found 24x24.`

- [ ] **Step 3: Make the minimal XAML change**

In `NoteStitch/MainWindow.xaml`, change only the nested image dimensions:

```xml
<Image Source="ms-appx:///Assets/notes.png"
       Width="40"
       Height="40"
       HorizontalAlignment="Center"
       VerticalAlignment="Center"/>
```

- [ ] **Step 4: Run the layout test and verify it passes**

Run:

```powershell
pwsh -NoProfile -File tests/ui/Validate-TitleBarLayout.ps1
```

Expected: `PASS: title-bar logo is 40x40 pixels.`

- [ ] **Step 5: Build and run the application regression tests**

Run:

```powershell
dotnet build NoteStitch/NoteStitch.csproj `
  -c Debug `
  --no-restore `
  -p:BaseOutputPath=artifacts/title-logo-verify/
dotnet run --project NoteStitch.Tests/NoteStitch.Tests.csproj --no-restore
git diff --check
```

Expected: the build succeeds with zero errors, both application regression
tests pass, and `git diff --check` is silent.

- [ ] **Step 6: Commit the verified title-bar change**

```powershell
git add NoteStitch/MainWindow.xaml tests/ui/Validate-TitleBarLayout.ps1
git commit -m "style: enlarge title bar logo"
```
