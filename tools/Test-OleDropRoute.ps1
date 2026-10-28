param([switch]$MonoTarget, [switch]$MonoX64, [uint32]$ExtendedStyle = 0, [string]$TargetProgram = '', [switch]$ExpectRejected, [switch]$ToggleTransparent)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputDirectory = Join-Path $projectRoot 'Logs/OleDropRoute'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$runtime = if ($MonoX64) { 'mono64' } elseif ($TargetProgram) { 'custom' } elseif ($MonoTarget) { 'mono32' } else { 'net' }
$caseName = "$runtime-$ExtendedStyle-isolated"
if ($ToggleTransparent) { $caseName += '-toggle' }
$service = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Scripts/Windows/PetFileDropTarget.cs') -Raw
foreach ($method in @('DragEnter', 'DragOver', 'DragLeave', 'Drop')) {
    $pattern = '(int IFileDropTarget\.' + $method + '\([^\r\n]+\)\s*\{)'
    $service = [regex]::Replace($service, $pattern, ('$1' + [Environment]::NewLine + '            Console.WriteLine("EVENT ' + $method + ' thread=" + GetCurrentThreadId());'))
}
$service = $service.Replace('Marshal.ThrowExceptionForHR(RegisterDragDrop(hwnd, target)); registered = true;',
    'Marshal.ThrowExceptionForHR(RegisterDragDrop(hwnd, target)); registered = true; Console.WriteLine("REGISTER thread=" + GetCurrentThreadId());')
$service = $service.Replace('received = getData(data, format, medium) >= 0;',
    'int dataResult = getData(data, format, medium); received = dataResult >= 0; Console.WriteLine("GETDATA hr=0x" + dataResult.ToString("X8") + " pointerSize=" + IntPtr.Size + " mediumType=" + Marshal.ReadInt32(medium));')
$service = $service.Replace('uint count = DragQueryFile(drop, uint.MaxValue, null, 0);',
    'uint count = DragQueryFile(drop, uint.MaxValue, null, 0); Console.WriteLine("HDROP count=" + count);')
$traceFile = Join-Path $outputDirectory "PetFileDropTarget.$caseName.Trace.cs"
Set-Content -LiteralPath $traceFile -Value $service -Encoding UTF8
$outputFile = Join-Path $outputDirectory "OleDropRouteProbe-$caseName.exe"
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe "/out:$outputFile" (Join-Path $PSScriptRoot 'OleDropRouteProbe.cs') $traceFile
if ($LASTEXITCODE -ne 0) { throw 'OLE 路由诊断编译失败' }
$launcher = Join-Path $outputDirectory 'OleDropRouteDesktop.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe "/out:$launcher" (Join-Path $PSScriptRoot 'OleDropRouteDesktop.cs')
if ($LASTEXITCODE -ne 0) { throw '隔离桌面诊断编译失败' }
$prefix = @()
$program = if ($MonoX64) {
    $prefix = @(
        (Join-Path $projectRoot 'Builds/Windows/MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll'),
        'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Data/MonoBleedingEdge/lib',
        (Join-Path $projectRoot 'Builds/Windows/MonoBleedingEdge/etc'),
        (Join-Path $projectRoot 'Logs/EmbeddedMonoX64/MonoCheckEntry.exe')
    )
    Join-Path $projectRoot 'Logs/EmbeddedMonoX64/MonoCheckHost.exe'
} elseif ($TargetProgram) { $TargetProgram } elseif ($MonoTarget) {
    'C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Data/MonoBleedingEdge/bin/mono.exe'
} else { $outputFile }
$readyFile = Join-Path $outputDirectory "$caseName-$([guid]::NewGuid().ToString('N')).ready"
$targetLog = Join-Path $outputDirectory "$caseName-target.log"
$sourceLog = Join-Path $outputDirectory "$caseName-source.log"
$encodedPrefix = if ($prefix.Count -gt 0) { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($prefix -join "`0"))) } else { '-' }
$expected = if ($ExpectRejected) { 'reject' } else { 'accept' }
$toggle = if ($ToggleTransparent) { 'toggle' } else { 'keep' }
& $launcher $program $outputFile $readyFile $targetLog $sourceLog $ExtendedStyle $encodedPrefix $expected $toggle
if ($LASTEXITCODE -ne 0) { throw '真实跨进程 OLE 拖放检查失败；参见 Logs/OleDropRoute。' }
