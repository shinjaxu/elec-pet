param([switch]$Mono)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputDirectory = Join-Path $projectRoot 'Logs/FileDropValidation'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$sources = @(Join-Path $PSScriptRoot 'WindowsDropChecks.cs')
$service = Join-Path $projectRoot 'Assets/Scripts/Windows/PetFileDropTarget.cs'
if (Test-Path -LiteralPath $service) { $sources += $service }
$outputFile = Join-Path $outputDirectory 'WindowsDropChecks.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe "/out:$outputFile" $sources
if ($LASTEXITCODE -ne 0) { throw '文件拖放检查编译失败' }
if ($Mono) {
    & 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Data/MonoBleedingEdge/bin/mono.exe' $outputFile
} else { & $outputFile }
if ($LASTEXITCODE -ne 0) { throw '文件拖放检查失败' }
