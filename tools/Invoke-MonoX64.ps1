param([Parameter(Mandatory = $true)][string]$Assembly, [string[]]$Arguments = @())
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputDirectory = Join-Path $projectRoot 'Logs/EmbeddedMonoX64'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$hostFile = Join-Path $outputDirectory 'MonoCheckHost.exe'
$hostSource = Join-Path $PSScriptRoot 'MonoCheckHost.cs'
if (!(Test-Path -LiteralPath $hostFile) -or (Get-Item -LiteralPath $hostSource).LastWriteTimeUtc -gt (Get-Item -LiteralPath $hostFile).LastWriteTimeUtc) {
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x64 "/out:$hostFile" $hostSource
    if ($LASTEXITCODE -ne 0) { throw 'x64 Mono 宿主编译失败' }
}
$entryFile = Join-Path $outputDirectory 'MonoCheckEntry.exe'
$entrySource = Join-Path $PSScriptRoot 'MonoCheckEntry.cs'
if (!(Test-Path -LiteralPath $entryFile) -or (Get-Item -LiteralPath $entrySource).LastWriteTimeUtc -gt (Get-Item -LiteralPath $entryFile).LastWriteTimeUtc) {
    & "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:anycpu "/out:$entryFile" $entrySource
    if ($LASTEXITCODE -ne 0) { throw 'Mono 检查入口编译失败' }
}
$runtimeFile = Join-Path $projectRoot 'Builds/Windows/MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll'
$monoLibrary = 'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Data/MonoBleedingEdge/lib'
$monoConfiguration = Join-Path $projectRoot 'Builds/Windows/MonoBleedingEdge/etc'
& $hostFile $runtimeFile $monoLibrary $monoConfiguration $entryFile (Resolve-Path -LiteralPath $Assembly).Path @Arguments
if ($LASTEXITCODE -ne 0) { throw "x64 Mono 检查失败，退出码=$LASTEXITCODE" }
