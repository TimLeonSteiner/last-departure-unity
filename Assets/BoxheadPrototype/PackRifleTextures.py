"""Pack the supplied CC0 metallic and roughness maps for Unity URP."""
from pathlib import Path
from PIL import Image
import numpy as np
root = Path(__file__).resolve().parent / 'ThirdParty/M4A1/M4A1'
metallic = np.asarray(Image.open(root/'M4A1_Metallic.png').convert('RGB'))
roughness = np.asarray(Image.open(root/'M4A1_Roughness.png').convert('L'))
Image.fromarray(np.dstack((metallic, 255-roughness)), 'RGBA').save(root/'M4A1_MetallicSmoothness.png')
