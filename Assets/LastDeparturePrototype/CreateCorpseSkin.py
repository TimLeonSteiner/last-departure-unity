"""Original mottled skin texture for the game's existing undead characters."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

rng = np.random.default_rng(2707)
size = 512
def noise(resolution, blur):
    source = Image.fromarray(rng.integers(0, 256, (resolution, resolution), dtype=np.uint8))
    return np.asarray(source.resize((size, size), Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(blur)), dtype=float) / 255

broad, spots, pores = noise(12, 8), noise(45, 2), noise(256, .4)
rgb = np.zeros((size, size, 3))
rgb[:] = [149, 154, 133]
rgb += ((broad - .5) * 48 + (spots - .5) * 26 + (pores - .5) * 9)[..., None]
purple = np.clip((.38 - broad) * 3, 0, .45)
rgb = rgb * (1 - purple[..., None]) + np.array([104, 93, 103]) * purple[..., None]
dark = np.clip((.32 - spots) * 2, 0, .25)
rgb *= 1 - dark[..., None]
out = Path(__file__).parent / 'Textures' / 'CorpseSkin.png'
Image.fromarray(np.uint8(np.clip(rgb, 0, 255))).save(out)
print(out.name + ': 512px original mottled ash/olive skin')
