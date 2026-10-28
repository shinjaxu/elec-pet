from pathlib import Path
from PIL import Image


root = Path(__file__).resolve().parents[1] / "docs" / "previews"
paths = sorted((root / "locomotion-frames").glob("*.png"))
if len(paths) != 240:
    raise SystemExit("请先在 Unity 执行：桌宠 → 导出转身步态动态预览，生成 240 帧。")

# 从整个转身过程取样建立统一调色板，避免各帧独立量化造成颜色闪动。
samples = Image.new("RGB", (512 * 4, 512 * 2))
for index in range(8):
    with Image.open(paths[index * 30]) as frame:
        samples.paste(frame, ((index % 4) * 512, (index // 4) * 512))
palette = samples.quantize(colors=256)
frames = []
for path in paths:
    with Image.open(path) as frame:
        frames.append(frame.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE))

output = root / "locomotion.gif"
# GIF 以 10 毫秒为单位，三帧合计 100 毫秒，保持实际 30 帧每秒的八秒步态。
frames[0].save(output, save_all=True, append_images=frames[1:],
               duration=[30, 40, 30] * 80, loop=0, optimize=False)
with Image.open(output) as result:
    print(f"已导出 {result.n_frames} 帧动态预览：{output}")
