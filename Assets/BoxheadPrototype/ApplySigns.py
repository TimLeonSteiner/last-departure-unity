from pathlib import Path
p=Path('BoxheadPrototype/Scripts/DepotWarehouse.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('Label(g, wall, "D27 / LOADING " + (side + 1), new Vector3(0, 4.7f, 17.24f), .065f, Color.white, false);','LoadingSign(g, wall, side + 1);');p.write_text(s,encoding='utf-8')
p=Path('BoxheadPrototype/Scripts/DepotModels.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('Label(g, billboard, "A BETTER TOMORROW\\nDUSTLINE INDUSTRIES", new Vector3(0, 6, -.16f), .055f, new Color(.25f, .29f, .24f), false);','HighwaySign(g, billboard);');p.write_text(s,encoding='utf-8')
