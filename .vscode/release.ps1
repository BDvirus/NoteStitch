$ErrorActionPreference = "Stop"

$csproj = Join-Path $PSScriptRoot "..\NoteStitch\NoteStitch.csproj"
$csproj = Resolve-Path $csproj

[xml]$xml = Get-Content $csproj
$current = [version]$xml.Project.PropertyGroup.Version
$next    = "{0}.{1}.{2}" -f $current.Major, $current.Minor, ($current.Build + 1)

$xml.Project.PropertyGroup.Version = $next
$xml.Save($csproj)

Write-Host "Bumped to v$next" -ForegroundColor Cyan

git add "$csproj"
git commit -m "chore: bump version to $next"
git tag "v$next"
git push origin main "v$next"

Write-Host "Released v$next" -ForegroundColor Green
