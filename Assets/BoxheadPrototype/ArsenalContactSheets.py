from PIL import Image,ImageDraw
from pathlib import Path
root=Path('BoxheadPrototype/ArsenalReview')
for id in (12,13,14,15,16,17,18,21):
 paths=sorted(root.glob(f'Timeline-{id}-*.png'),key=lambda p:int(p.stem.split('-')[-1]))
 if not paths:continue
 sheet=Image.new('RGB',(6*320,((len(paths)+5)//6)*225),(25,28,30));draw=ImageDraw.Draw(sheet)
 for i,p in enumerate(paths):
  x=(i%6)*320;y=(i//6)*225;im=Image.open(p);im.thumbnail((320,200));sheet.paste(im,(x,y));draw.text((x+8,y+204),p.stem,fill='white')
 sheet.save(root/f'Timeline-{id}.jpg')
