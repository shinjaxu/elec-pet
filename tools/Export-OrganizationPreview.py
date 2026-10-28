from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1] / "docs" / "previews" / "organization"
paths = sorted((root / "frames").glob("*.png"))
if len(paths) != 240:
    raise SystemExit("请先在 Unity 执行：桌宠 → 导出整理搬运动画，生成240帧。")

# 共用调色板避免逐帧量化闪烁，裁出接触区域便于检查嘴部与图标的联动。
box = (140, 130, 620, 370)
samples = Image.new("RGB", (480 * 4, 240 * 2))
for index in range(8):
    with Image.open(paths[index * 30]) as frame:
        samples.paste(frame.crop(box), ((index % 4) * 480, (index // 4) * 240))
palette = samples.quantize(colors=256)
frames = []
for path in paths:
    with Image.open(path) as frame:
        frames.append(frame.crop(box).quantize(palette=palette, dither=Image.Dither.NONE))
output = root / "carry.gif"
frames[0].save(output, save_all=True, append_images=frames[1:],
               duration=[30, 40, 30] * 80, loop=0, optimize=False)
with Image.open(output) as result:
    print(f"已导出动画：{output}，{result.n_frames}个变化帧。")
