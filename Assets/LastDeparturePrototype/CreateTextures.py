"""Small original, tileable weathering textures for the depot materials."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent / "Textures"
root.mkdir(exist_ok=True)
rng = np.random.default_rng(27)
for name in ["Concrete", "OxidizedMetal", "Canvas"]:
    size = 512
    grain = rng.normal(0, 7 if name == "Concrete" else 4, (size, size))
    low = np.asarray(Image.fromarray(rng.integers(130, 215, (32, 32), dtype=np.uint8)).resize((size, size), Image.Resampling.BILINEAR))
    shade = np.clip(low * .45 + 143 + grain, 0, 255).astype(np.uint8)
    image = Image.fromarray(np.stack([shade, shade, shade], axis=-1))
    draw = ImageDraw.Draw(image)
    if name == "Concrete":
        # Hairline cracks, dust inclusions and worn patches, not a photo reference.
        for _ in range(30):
            x, y = rng.integers(0, size, 2)
            points = [(int(x), int(y))]
            for _ in range(8):
                x += rng.integers(-12, 13); y += rng.integers(2, 14)
                points.append((int(x), int(y)))
            draw.line(points, fill=(126, 126, 126), width=1)
        for _ in range(650):
            x, y = rng.integers(0, size, 2); r = int(rng.integers(1, 3))
            draw.ellipse((x, y, x + r, y + r), fill=(164, 164, 164))
    elif name == "OxidizedMetal":
        for _ in range(500):
            x, y = rng.integers(0, size, 2); r = int(rng.integers(1, 8))
            draw.ellipse((x, y, x + r, y + r // 2), fill=(156, 133, 108))
        for x in range(0, size, 128):
            draw.line((x, 0, x, size), fill=(125, 135, 130), width=2)
            for y in range(16, size, 96):
                draw.ellipse((x + 8, y, x + 13, y + 5), fill=(125, 125, 125))
    else:
        for line in range(0, size, 4):
            draw.line((line, 0, line, size), fill=(204, 204, 204))
            draw.line((0, line, size, line), fill=(190, 190, 190))
    image.save(root / (name + ".png"))
print("Three 512px procedural material textures written.")
