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
