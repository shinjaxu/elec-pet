$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$coreFiles = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Core') -Filter '*.cs' | ForEach-Object FullName)
if ($coreFiles.Count -eq 0) { throw 'RED: Desktop motion is not implemented yet.' }
$sources = $coreFiles + (Join-Path $projectRoot 'Assets/Tests/EditMode/CoreChecks.cs') + (Join-Path $projectRoot 'Assets/Tests/EditMode/BehaviorChecks.cs') + (Join-Path $projectRoot 'Assets/Tests/EditMode/LifeChecks.cs')
$sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/SaveChecks.cs')
$sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/OrganizationChecks.cs')
$sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/CarryChecks.cs')
$sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/KeyboardIdleChecks.cs')
$sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/FeedingChecks.cs')
$sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/ScreenCompanionChecks.cs')
$sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/MouseFetchChecks.cs')
Add-Type -Path $sources
$checks = [ElecPet.Tests.CoreChecks]::Run()
Write-Output "PASS: $checks core behavior checks"
Write-Output "PASS: $([ElecPet.Tests.CoreChecks]::RunMultiMonitor()) multi-monitor boundary checks"
$behaviors = [ElecPet.Tests.BehaviorChecks]::Run()
Write-Output "PASS: $behaviors interaction and autonomy checks"
$life = [ElecPet.Tests.LifeChecks]::Run()
Write-Output "PASS: $life companionship checks"

$saves = [ElecPet.Tests.SaveChecks]::Run()
Write-Output "PASS: $saves save checks"
$organization = [ElecPet.Tests.OrganizationChecks]::Run()
Write-Output "PASS: $organization organization checks"
$carry = [ElecPet.Tests.CarryChecks]::Run()
Write-Output "PASS: $carry carry checks"
Write-Output "PASS: $([ElecPet.Tests.KeyboardIdleChecks]::Run()) keyboard idle checks"
Write-Output "PASS: $([ElecPet.Tests.FeedingChecks]::Run()) feeding checks"
Write-Output "PASS: $([ElecPet.Tests.ScreenCompanionChecks]::Run()) screen companion checks"
Write-Output "PASS: $([ElecPet.Tests.MouseFetchChecks]::Run()) mouse fetch checks"
