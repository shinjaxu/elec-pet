param([switch]$Pixels)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputDirectory = Join-Path $projectRoot 'Logs/IconFollowValidation'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$sources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Core') -Filter '*.cs' | ForEach-Object FullName)
$sources += @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Windows') -Filter '*.cs' | ForEach-Object FullName)
$sources += Join-Path $PSScriptRoot 'DesktopVisualChecks.cs'
$framework = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319"
$outputFile = Join-Path $outputDirectory 'DesktopVisualChecks.exe'
& "$framework/csc.exe" /nologo /target:exe "/out:$outputFile" /r:System.Core.dll /r:System.Drawing.dll "/r:$framework/WPF/UIAutomationClient.dll" "/r:$framework/WPF/UIAutomationTypes.dll" "/r:$framework/WPF/WindowsBase.dll" $sources
if ($LASTEXITCODE -ne 0) { throw 'Visual checks compilation failed' }
if ($Pixels) { & $outputFile --pixels } else { & $outputFile }
if ($LASTEXITCODE -ne 0) { throw 'Icon display checks failed' }
