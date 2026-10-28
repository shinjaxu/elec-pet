$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Core') -Filter '*.cs' | ForEach-Object FullName)
$sources += Join-Path $projectRoot 'Assets/Tests/EditMode/MouseFetchChecks.cs'
Add-Type -Path $sources
Write-Output "PASS: $([ElecPet.Tests.MouseFetchChecks]::Run()) mouse fetch checks"
