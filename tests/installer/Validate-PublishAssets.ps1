param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'
$publishRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
$requiredAssets = @(
    'Assets\notes.png',
    'Assets\icon.ico'
)

foreach ($relativePath in $requiredAssets) {
    $assetPath = Join-Path $publishRoot $relativePath

    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "Published application is missing a required image asset: $assetPath"
    }

    if ((Get-Item -LiteralPath $assetPath).Length -le 0) {
        throw "Published image asset is empty: $assetPath"
    }
}

Write-Host 'PASS: published application contains its UI and tray images.'
