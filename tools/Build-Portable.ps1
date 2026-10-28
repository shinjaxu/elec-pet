$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildDirectory = Join-Path $projectRoot 'Builds/Windows'
$packagingDirectory = Join-Path $PSScriptRoot 'packaging'
$outputDirectory = Join-Path $projectRoot 'Builds/Portable'
$workDirectory = Join-Path $projectRoot 'Logs/PortablePackaging'
$archivePath = Join-Path $workDirectory 'ElecPet.7z'
$outputPath = Join-Path $outputDirectory 'ElecPet-Portable.exe'

# 完整保留 Unity 的目录结构；内置 Mono，无需用户安装运行时。
foreach ($required in @('ElecPet.exe', 'UnityPlayer.dll', 'ElecPet_Data/globalgamemanagers',
    'ElecPet_Data/Managed/ElecPet.dll', 'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $buildDirectory $required) -PathType Leaf)) {
        throw "缺少 Windows 构建文件：${required}。请先在 Unity 中执行「桌宠 → 构建 Windows x64」。"
    }
}
New-Item -ItemType Directory -Path $outputDirectory, $workDirectory -Force | Out-Null

# 显式声明普通用户权限，避免原始 SFX 被 Windows 误识别为需要提权的安装器。
$sfxPath = Join-Path $workDirectory 'ElecPet.sfx'
Copy-Item -LiteralPath (Join-Path $packagingDirectory '7zSD.sfx') -Destination $sfxPath -Force
Add-Type -Namespace ElecPet.Packaging -Name NativeResource -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern System.IntPtr BeginUpdateResource(string path, bool deleteExisting);
[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern bool UpdateResource(System.IntPtr handle, System.IntPtr type, System.IntPtr name, ushort language, byte[] data, uint size);
[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern bool EndUpdateResource(System.IntPtr handle, bool discard);
'@
$manifest = [IO.File]::ReadAllBytes((Join-Path $packagingDirectory 'app.manifest'))
$resource = [ElecPet.Packaging.NativeResource]::BeginUpdateResource($sfxPath, $false)
if ($resource -eq [IntPtr]::Zero) { throw (New-Object ComponentModel.Win32Exception) }
$resourceUpdated = [ElecPet.Packaging.NativeResource]::UpdateResource($resource, [IntPtr]24, [IntPtr]1, 0, $manifest, $manifest.Length)
if (!$resourceUpdated) {
    $resourceError = New-Object ComponentModel.Win32Exception
    [ElecPet.Packaging.NativeResource]::EndUpdateResource($resource, $true) | Out-Null
    throw $resourceError
}
$resourceSaved = [ElecPet.Packaging.NativeResource]::EndUpdateResource($resource, $false)
if (!$resourceSaved) { throw (New-Object ComponentModel.Win32Exception) }

if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath }

Push-Location $buildDirectory
try {
    & (Join-Path $packagingDirectory '7zr.exe') a -t7z -m0=LZMA2 -mx=7 -mmt=2 $archivePath '.\*'
    if ($LASTEXITCODE -ne 0) { throw 'Windows 构建压缩失败。' }
}
finally { Pop-Location }

# 官方原生模块先释放到用户临时目录，等待桌宠退出后自动清理，不安装或提权。
$config = @'
;!@Install@!UTF-8!
Title="ElecPet"
Progress="no"
RunProgram="ElecPet.exe"
;!@InstallEnd@!
'@
$configPath = Join-Path $workDirectory 'sfx-config.txt'
[IO.File]::WriteAllText($configPath, $config + "`n", (New-Object Text.UTF8Encoding $false))
$output = [IO.File]::Create($outputPath)
try {
    foreach ($part in @($sfxPath, $configPath, $archivePath)) {
        $partStream = [IO.File]::OpenRead($part)
        try { $partStream.CopyTo($output) }
        finally { $partStream.Dispose() }
    }
}
finally { $output.Dispose() }

& (Join-Path $packagingDirectory '7zr.exe') t $outputPath
if ($LASTEXITCODE -ne 0) { throw '单文件 EXE 完整性校验失败。' }
Write-Output "免安装单文件：$outputPath"
Get-FileHash -LiteralPath $outputPath -Algorithm SHA256
