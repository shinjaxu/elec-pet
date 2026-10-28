param([switch]$VisibleFixture)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputDirectory = Join-Path $projectRoot 'Logs/ScreenCapture'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$outputFile = Join-Path $outputDirectory 'ScreenCaptureChecks.exe'
$sources = @((Join-Path $PSScriptRoot 'ScreenCaptureChecks.cs'), (Join-Path $projectRoot 'Assets/Scripts/Core/DesktopGeometry.cs'))
$observer = Join-Path $projectRoot 'Assets/Scripts/Windows/WindowsScreenObserver.cs'
if (Test-Path -LiteralPath $observer) { $sources += $observer }
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe "/out:$outputFile" $sources
if ($LASTEXITCODE -ne 0) { throw '屏幕观察检查编译失败。' }
if ($VisibleFixture) { & $outputFile --visible-fixture } else { & $outputFile }
if ($LASTEXITCODE -ne 0) { throw '屏幕观察检查失败；未读取实际用户画面。' }
