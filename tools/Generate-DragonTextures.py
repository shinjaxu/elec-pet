from math import atan2, cos, exp, floor, pi, sin, sqrt
from pathlib import Path
from PIL import Image


# 无缝、低对比度的细鳞只提供近看质感；远看由 mipmap 收敛，避免桌面尺寸闪烁。
root = Path(__file__).resolve().parents[1] / "Assets" / "Resources"
width, height = 512, 256
heights = []
albedo = Image.new("RGB", (width, height))
for y in range(height):
    row = floor(y / 16)
    for x in range(width):
        dx = ((x + (row % 2) * 8) % 16 - 8) / 8
        dy = (y % 16 - 8) / 8
        distance = sqrt(dx * dx + dy * dy * 1.15)
        dome = exp(-distance * distance * 2.5)
        heights.append(dome)
        shade = round(247 + dome * 7)
        albedo.putpixel((x, y), (shade, shade, shade))

normal = Image.new("RGB", (width, height))
for y in range(height):
    for x in range(width):
        dx = (heights[y * width + (x + 1) % width] - heights[y * width + (x - 1) % width]) * .6
        dy = (heights[((y + 1) % height) * width + x] - heights[((y - 1) % height) * width + x]) * .6
        length = sqrt(dx * dx + dy * dy + 1)
        normal.putpixel((x, y), (round(127.5 - dx / length * 127.5),
                                round(127.5 + dy / length * 127.5), round(127.5 + 127.5 / length)))

albedo.save(root / "DragonScales.png")
normal.save(root / "DragonScalesNormal.png")

# 经纬映射下的径向纤维与深色外缘，保留小尺寸下清楚的绿色眼形。
iris = Image.new("RGB", (width, height))
for y in range(height):
    latitude = pi * y / (height - 1)
    for x in range(width):
        longitude = 2 * pi * x / (width - 1)
        sx, sy = sin(latitude) * cos(longitude), cos(latitude)
        radius = min(1, sqrt(sx * sx + sy * sy))
        angle = atan2(sy, sx)
        fiber = .92 + .055 * sin(angle * 61 + radius * 15) + .025 * sin(angle * 103 - radius * 29)
        rim = 1 - .40 * max(0, (radius - .78) / .22)
        iris.putpixel((x, y), tuple(round(min(255, value * fiber * rim)) for value in (246, 255, 206)))
iris.save(root / "DragonIris.png")
print("已生成皮肤颜色和法线纹理：512×256，均可无缝重复。")
