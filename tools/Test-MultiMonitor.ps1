$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sources = @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Core') -Filter '*.cs' | ForEach-Object FullName)
$sources += @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Windows') -Filter '*.cs' | ForEach-Object FullName)
Add-Type -Path $sources
Add-Type -AssemblyName System.Windows.Forms

# 只调整测试自建的隐藏窗口，不操作用户窗口或桌面图标。
$form = New-Object System.Windows.Forms.Form
try {
    $window = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([ElecPet.Windows.NativeWindow])
    $flags = [Reflection.BindingFlags]'Instance,NonPublic'
    [ElecPet.Windows.NativeWindow].GetField('hwnd', $flags).SetValue($window, $form.Handle)
    $window.RefreshBounds()
    $expected = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $actual = $window.Bounds
    if ($actual.X -ne $expected.X -or $actual.Y -ne $expected.Y -or $actual.Width -ne $expected.Width -or $actual.Height -ne $expected.Height) {
        throw "窗口没有覆盖全部屏幕：actual=$($actual.X),$($actual.Y),$($actual.Width),$($actual.Height); expected=$expected"
    }
    if ($form.DesktopBounds -ne $expected) { throw "原生窗口定位不匹配：actual=$($form.DesktopBounds); expected=$expected" }
    $work = [System.Drawing.Rectangle]::Empty
    foreach ($screen in [System.Windows.Forms.Screen]::AllScreens) {
        if ($work.IsEmpty) { $work = $screen.WorkingArea }
        else { $work = [System.Drawing.Rectangle]::Union($work, $screen.WorkingArea) }
    }
    $actual = $window.WorkArea
    if ($actual.X -ne $work.X -or $actual.Y -ne $work.Y -or $actual.Width -ne $work.Width -or $actual.Height -ne $work.Height) {
        throw "活动范围没有覆盖全部屏幕工作区：actual=$($actual.X),$($actual.Y),$($actual.Width),$($actual.Height); expected=$work"
    }
    Write-Output "PASS: $([System.Windows.Forms.Screen]::AllScreens.Count) monitors; virtual desktop=$expected; work area=$work"

    # 将宠物拖向整个虚拟桌面的网格点，独立按实际工作区交集面积验证身体不会进入空隙。
    $motion = New-Object ElecPet.Core.PetMotion($window.Bounds, 88, $window.WorkAreas)
    $boundaryChecks = 0
    for ($x = $expected.Left; $x -le $expected.Right; $x += 131) {
        for ($y = $expected.Top; $y -le $expected.Bottom; $y += 89) {
            $motion.DragTo((New-Object ElecPet.Core.Point2($x, $y)))
            $point = $motion.Position
            $covered = 0.0
            foreach ($screen in [System.Windows.Forms.Screen]::AllScreens) {
                $screenWork = $screen.WorkingArea
                $overlapWidth = [Math]::Max(0, [Math]::Min($point.X + 88, $screenWork.Right) - [Math]::Max($point.X - 88, $screenWork.Left))
                $overlapHeight = [Math]::Max(0, [Math]::Min($point.Y + 88, $screenWork.Bottom) - [Math]::Max($point.Y - 88, $screenWork.Top))
                $covered += $overlapWidth * $overlapHeight
            }
            if ([Math]::Abs($covered - 176 * 176) -gt .001) { throw "宠物安全包围盒越出真实屏幕：$($point.X),$($point.Y)" }
            $boundaryChecks++
        }
    }
    Write-Output "PASS: $boundaryChecks drag positions keep the complete pet footprint inside real monitor work areas"

    # 使用左上方副屏的原点，检查短点击消息与全局鼠标采用相同坐标系。
    [ElecPet.Windows.NativeWindow].GetField('<Bounds>k__BackingField', $flags).SetValue($window, (New-Object ElecPet.Core.DesktopRect(-1920, -1080, 3840, 2160)))
    $callback = [ElecPet.Windows.NativeWindow].GetMethod('HandleMessage', $flags)
    foreach ($message in @(0x201, 0x202)) {
        $callback.Invoke($window, @([IntPtr]::Zero, [uint32]$message, [IntPtr]::Zero, [IntPtr](34 * 65536 + 12))) | Out-Null
    }
    foreach ($point in @($window.TakeLeftPress(), $window.TakeLeftRelease())) {
        if ($point.X -ne -1908 -or $point.Y -ne -1046) { throw "副屏点击坐标错误：$($point.X),$($point.Y)" }
    }
    Write-Output 'PASS: press and release keep global coordinates on a left/top monitor'
}
finally { if ($null -ne $form) { $form.Dispose() } }
