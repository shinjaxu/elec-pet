from pathlib import Path
from PIL import Image, ImageDraw
root = Path('docs/previews/mouse-fetch')
paths = sorted((root / 'frames').glob('*.png'))
frames = [Image.open(path).convert('RGB') for path in paths]
frames[0].save(root / 'mouse-fetch.gif', save_all=True, append_images=frames[1:], duration=[30, 40, 30] * 75, loop=0)
contact = Image.new('RGB', (1200, 750), '#e8edf5')
for i, index in enumerate([15, 35, 60, 85, 115, 175]):
    contact.paste(frames[index].resize((400, 250)), ((i % 3) * 400, (i // 3) * 375))
    ImageDraw.Draw(contact).text(((i % 3) * 400 + 12, (i // 3) * 375 + 255), str(index / 30) + 's', fill='black')
contact.save(root / 'contact.png')
print(len(frames), 'frames')
