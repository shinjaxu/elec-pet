from pathlib import Path
from PIL import Image


root = Path(__file__).resolve().parents[1] / "docs" / "previews" / "feeding"
for folder, output in (("frames", "feeding.gif"), ("failed-frames", "failed.gif")):
    paths = sorted((root / folder).glob("*.png"))
    if len(paths) != 151:
        raise SystemExit("请先在 Unity 执行：桌宠 → 导出文件喂食预览，生成每段 151 帧。")
    samples = Image.new("RGB", (640 * 3, 480 * 2))
    for index, frame_index in enumerate((0, 25, 50, 75, 100, 130)):
        with Image.open(paths[frame_index]) as frame:
            samples.paste(frame, ((index % 3) * 640, (index // 3) * 480))
    palette = samples.quantize(colors=256)
    frames = []
    for path in paths:
        with Image.open(path) as frame:
            frames.append(frame.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE))
    # 全片共用调色板，维持三十帧步进，避免皮肤与粒子因逐帧量化闪烁。
    frames[0].save(root / output, save_all=True, append_images=frames[1:],
                   duration=[30, 40, 30] * 50 + [30], loop=0, optimize=False)
    with Image.open(root / output) as result:
        print(f"已导出 {result.n_frames} 帧预览：{root / output}")
