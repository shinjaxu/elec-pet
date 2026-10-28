param([switch]$Write, [string]$ItemId, [switch]$Simulated)
$ErrorActionPreference = 'Stop'
function Invoke-IconProbe($Session, $Item, [scriptblock]$IdleSeconds, [scriptblock]$Delay) {
    $last = $Item.Position
    try {
        for ($frame = 0; $frame -le 60; $frame++) {
            if ((& $IdleSeconds) -lt 30) { throw '用户输入中断写入探测。' }
            if (![ElecPet.Core.DesktopOrganization]::Near($last, $Session.Position($Item.Id))) { throw '测试图标位置改变，停止探测。' }
            $t = $frame / 60.0
            $x = $Item.Position.X + [Math]::Sin($t * [Math]::PI) * 40
            $point = [ElecPet.Core.Point2]::new($x, $Item.Position.Y)
            $actual = $Session.Move($Item.Id, $point)
            if (![ElecPet.Core.DesktopOrganization]::Near($actual, $point)) { throw 'Shell 未接受连续定位。' }
            $last = $actual
            & $Delay
        }
    } finally {
        # 正常轨迹末帧已经回位；中断清理只恢复设置，不覆盖用户的新位置。
        $Session.RestoreAlignment()
    }
}
$projectRoot = Split-Path $PSScriptRoot -Parent
$sources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Core') -Filter '*.cs' | ForEach-Object FullName)
$sources += @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Windows') -Filter '*.cs' | ForEach-Object FullName)
if ($Simulated) {
    $sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/IconWorkerChecks.cs')
    $sources += (Join-Path $projectRoot 'Assets/Tests/EditMode/OrganizerChecks.cs')
    $sources += (Join-Path $projectRoot 'Assets/Scripts/Runtime/PetDesktopOrganizer.cs')
    $sources += (Join-Path $projectRoot 'Assets/Scripts/Runtime/DesktopOrganizationPreview.cs')
}
Add-Type -Path $sources
if ($Write -and !$ItemId) { throw '写入探测必须明确指定测试图标 ItemId。' }
if ($Simulated) {
    Write-Output "PASS: $([ElecPet.Tests.IconWorkerChecks]::Run()) worker checks"
    foreach ($scenario in @('busy', 'interrupted', 'conflict')) {
        $fixture = [pscustomobject]@{ Current = [ElecPet.Core.Point2]::new(10,20); Writes=0; Restores=0; Calls=0 }
        $fixture | Add-Member ScriptMethod Position { param($id) return $this.Current }
        $fixture | Add-Member ScriptMethod Move { param($id,$point) $this.Writes++; $this.Current=$point; return $point }
        $fixture | Add-Member ScriptMethod RestoreAlignment { $this.Restores++ }
        $item = [pscustomobject]@{ Id='test'; Position=$fixture.Current }
        $idle = { $fixture.Calls++; if ($scenario -eq 'busy' -or ($scenario -eq 'interrupted' -and $fixture.Calls -gt 3)) { 0 } else { 30 } }
        $delay = { if ($scenario -eq 'conflict') { $fixture.Current=[ElecPet.Core.Point2]::new(999,20) } }
        try { Invoke-IconProbe $fixture $item $idle $delay } catch { }
        $expectedWrites = if ($scenario -eq 'busy') { 0 } elseif ($scenario -eq 'interrupted') { 3 } else { 1 }
        if ($fixture.Writes -ne $expectedWrites -or $fixture.Restores -ne 1) { throw "探测中断不能继续回位写入: $scenario writes=$($fixture.Writes)" }
        if ($scenario -eq 'conflict' -and $fixture.Current.X -ne 999) { throw '探测不能覆盖用户的新位置。' }
    }
    Write-Output 'PASS: 3 probe cancellation checks (no desktop writes)'
    Write-Output "PASS: $([ElecPet.Tests.OrganizerChecks]::Run()) organizer integration checks"
    exit
}
$keyboard = [ElecPet.Windows.WindowsKeyboardIdle]::new()
$session = $null
try {
    $session = [ElecPet.Windows.DesktopIcons]::OpenSession()
    $snapshot = $session.Read()
    if ($Write) {
        $item = $snapshot.Items | Where-Object Id -eq $ItemId | Select-Object -First 1
        if (!$item) { throw '指定项目不在当前桌面快照中。' }
        while ($keyboard.Seconds -lt 30) { Start-Sleep -Milliseconds 100 }
        Invoke-IconProbe $session $item { $keyboard.Seconds } { Start-Sleep -Milliseconds 33 }
    }
    Write-Output "PASS: $($snapshot.Items.Count) items; keyboardIdle=$([Math]::Round($keyboard.Seconds, 1))s; spacing=$($snapshot.Spacing.X)x$($snapshot.Spacing.Y); autoArrange=$($snapshot.AutoArrange); writes=$Write"
} finally { if ($session) { $session.Dispose() }; $keyboard.Dispose() }

