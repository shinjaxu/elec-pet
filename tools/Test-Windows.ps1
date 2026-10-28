$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Core') -Filter '*.cs' | ForEach-Object FullName)
$sources += @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Windows') -Filter '*.cs' | ForEach-Object FullName)
Add-Type -Path $sources
$cursor = [ElecPet.Windows.NativeWindow]::Cursor()
$icons = [ElecPet.Windows.DesktopIcons]::ReadPositions()
Write-Output "PASS: Windows adapter compiled; cursor available; $($icons.Count) icon positions read (no writes)."
