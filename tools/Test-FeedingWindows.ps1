param([switch]$Mono, [switch]$RecycleFixture)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputDirectory = Join-Path $projectRoot 'Logs/FeedingWindowsValidation'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$sources = @(Join-Path $PSScriptRoot 'WindowsFeedingChecks.cs')
$service = Join-Path $projectRoot 'Assets/Scripts/Windows/WindowsFileFeeding.cs'
if (Test-Path -LiteralPath $service) { $sources += $service }
$outputFile = Join-Path $outputDirectory 'WindowsFeedingChecks.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe "/out:$outputFile" $sources
if ($LASTEXITCODE -ne 0) { throw 'Windows feeding checks compilation failed' }
$checkArguments = @($outputDirectory)
if ($RecycleFixture) { $checkArguments += '--recycle-fixture' }
if ($Mono) {
    & 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Data/MonoBleedingEdge/bin/mono.exe' $outputFile @checkArguments
} else { & $outputFile @checkArguments }
if ($LASTEXITCODE -ne 0) { throw 'Windows feeding checks failed' }
