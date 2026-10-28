$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Add-Type -Path @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Core') -Filter '*.cs' | ForEach-Object FullName)
Add-Type -AssemblyName System.Drawing
$outputFile = Join-Path $projectRoot 'docs/previews/organization/heart-layout.png'
$bitmap = [Drawing.Bitmap]::new(1560, 540)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$titleFont = [Drawing.Font]::new('Microsoft YaHei', 17)
$labelFont = [Drawing.Font]::new('Microsoft YaHei', 9)
$ink = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(40, 52, 65))
$blue = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(70, 124, 178))
$border = [Drawing.Pen]::new([Drawing.Color]::FromArgb(204, 215, 225))
try {
    $graphics.Clear([Drawing.Color]::FromArgb(245, 247, 250))
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $amounts = @(12, 20, 8)
    $titles = @('12 个图标 · 完整轮廓', '20 个图标 · 多余排左上角', '8 个图标 · 对称稀疏轮廓')
    for ($panel = 0; $panel -lt 3; $panel++) {
        $items = [Collections.Generic.List[ElecPet.Core.DesktopItem]]::new()
        for ($i = 0; $i -lt $amounts[$panel]; $i++) {
            $id = '{0:D2}' -f ($i + 1)
            $items.Add([ElecPet.Core.DesktopItem]::new($id,$id,'.txt',$false,$false,
                [ElecPet.Core.Point2]::new(1200 - [Math]::Floor($i / 10) * 100, ($i % 10) * 100)))
        }
        $snapshot = [ElecPet.Core.DesktopSnapshot]::new($items,[ElecPet.Core.DesktopRect]::new(0,0,1300,1000),
            [ElecPet.Core.Point2]::new(0,0),[ElecPet.Core.Point2]::new(100,100),$false,'preview')
        $plan = [ElecPet.Core.DesktopOrganization]::Plan($snapshot)
        if ($plan.BlockedReason) { throw $plan.BlockedReason }
        $positions = @{}; foreach ($item in $items) { $positions[$item.Id] = $item.Position }
        foreach ($move in $plan.Moves) { $positions[$move.Id] = $move.To }
        # 直接绘制实际规划结果，示意图不读取或操作真实桌面。
        $left = 28 + $panel * 520
        $graphics.DrawString($titles[$panel],$titleFont,$ink,[single]$left,30)
        $graphics.FillRectangle([Drawing.Brushes]::White,$left,92,464,360)
        $graphics.DrawRectangle($border,$left,92,464,360)
        foreach ($item in $items) {
            $point = $positions[$item.Id]
            $x = [single]($left + 10 + $point.X * .34)
            $y = [single](104 + $point.Y * .34)
            $graphics.FillRectangle($blue,$x,$y,23,20)
            $graphics.DrawString($item.Name,$labelFont,$ink,$x,$y + 19)
        }
    }
    $graphics.DrawString('按名称排序 · 沿用桌面网格 · 爱心内部留白', $titleFont, $ink, 30, 482)
    $bitmap.Save($outputFile,[Drawing.Imaging.ImageFormat]::Png)
} finally {
    $border.Dispose(); $blue.Dispose(); $ink.Dispose(); $labelFont.Dispose(); $titleFont.Dispose()
    $graphics.Dispose(); $bitmap.Dispose()
}
Write-Output $outputFile
