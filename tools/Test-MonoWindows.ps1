param([switch]$WriteFixture)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputDirectory = Join-Path $projectRoot 'Logs/KeyboardOrganizationValidation'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$sources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Core') -Filter '*.cs' | ForEach-Object FullName)
$sources += @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Windows') -Filter '*.cs' | ForEach-Object FullName)
$sources += Join-Path $PSScriptRoot 'WindowsRuntimeChecks.cs'
$outputFile = Join-Path $outputDirectory 'WindowsRuntimeChecks.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe "/out:$outputFile" /r:System.Core.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Runtime checks compilation failed' }
$mono = 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Data/MonoBleedingEdge/bin/mono.exe'
if ($WriteFixture) { & $mono $outputFile --write-fixture } else { & $mono $outputFile }
if ($LASTEXITCODE -ne 0) { throw 'Unity Mono runtime checks failed' }
