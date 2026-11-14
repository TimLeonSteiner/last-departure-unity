using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class StormLandmarks
    {
        Material terminalPaint,terminalScreen;
        Material entranceTerminalSurface;

        void PlaceEntranceTerminal(float x)
        {
            var asset = Resources.Load<GameObject>("Spaceport/EntranceTerminal/Terminal");
            if (asset == null) return;

            if (entranceTerminalSurface == null)
            {
                entranceTerminalSurface = new Material(RuntimeShaders.Find("Universal Render Pipeline/Lit"))
                {
                    name = "Meshy entrance terminal",
                    mainTexture = Resources.Load<Texture2D>("Spaceport/EntranceTerminal/BaseColor")
                };
                entranceTerminalSurface.SetTexture("_BumpMap", Resources.Load<Texture2D>("Spaceport/EntranceTerminal/Normal"));
                entranceTerminalSurface.EnableKeyword("_NORMALMAP");
                entranceTerminalSurface.SetTexture("_EmissionMap", Resources.Load<Texture2D>("Spaceport/EntranceTerminal/AmberEmission"));
                entranceTerminalSurface.SetColor("_EmissionColor", new Color(1f, 1f, 1f) * .85f);
                entranceTerminalSurface.EnableKeyword("_EMISSION");
                entranceTerminalSurface.SetFloat("_Smoothness", .29f);
                entranceTerminalSurface.SetFloat("_Metallic", .18f);
                labelMaterials.Add(entranceTerminalSurface);
            }

            var mount = new GameObject("Entrance service terminal").transform;
            mount.SetParent(section, false);
            mount.localRotation = Quaternion.Euler(0, 180, 0);
            var model = Instantiate(asset, mount, false);
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) { Destroy(mount.gameObject); return; }

            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = entranceTerminalSurface;
                renderer.sharedMaterials = materials;
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                for (int ix = -1; ix <= 1; ix += 2)
                for (int iy = -1; iy <= 1; iy += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    var corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3(ix, iy, iz));
                    var point = mount.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            mount.localScale = Vector3.one * (1.72f / bounds.size.y);
            mount.localPosition = new Vector3(x, .015f, 21.1f);
        }

        void DetailedServiceTerminal(float x,int side)
        {
            if(terminalPaint==null)
            {
                terminalPaint=new Material(hull){name="Worn service terminal enamel"};
                terminalPaint.SetColor("_BaseColor",new Color(.25f,.29f,.27f));
                terminalPaint.SetFloat("_Metallic",.45f);terminalPaint.SetFloat("_Smoothness",.24f);labelMaterials.Add(terminalPaint);
                terminalScreen=new Material(teal){name="Dim amber terminal readout"};
                terminalScreen.SetColor("_BaseColor",new Color(.54f,.27f,.065f));terminalScreen.SetFloat("_Glow",.7f);labelMaterials.Add(terminalScreen);
            }
            const float z=21.1f;
            Box("Anchored terminal foot",new Vector3(x,.055f,z),new Vector3(.52f,.11f,.44f),steel);
            Box("Pedestal spine",new Vector3(x,.72f,z+.04f),new Vector3(.21f,1.3f,.18f),terminalPaint);
            Box("Pedestal service seam",new Vector3(x,.72f,z-.055f),new Vector3(.12f,.72f,.015f),dark);
            TerminalHousing(new Vector3(x,1.8f,z),new Vector3(.74f,1.08f,.30f),terminalPaint);
            Box("Recessed instrument face",new Vector3(x,1.81f,z-.156f),new Vector3(.63f,.92f,.025f),dark);
            Box("Screen metal surround",new Vector3(x-.035f,2.015f,z-.179f),new Vector3(.53f,.34f,.035f),steel);
            Box("Inset smoked display",new Vector3(x-.035f,2.015f,z-.201f),new Vector3(.46f,.265f,.009f),dark);
            Box("Display header",new Vector3(x-.085f,2.10f,z-.208f),new Vector3(.30f,.012f,.004f),terminalScreen);
            for(int row=0;row<4;row++)
            {
                Box("Status readout row",new Vector3(x-.14f,2.055f-row*.043f,z-.208f),new Vector3(.15f,.007f,.004f),terminalScreen);
                Box("Status meter",new Vector3(x+.045f,2.055f-row*.043f,z-.208f),new Vector3(.05f+row*.026f,.013f,.004f),terminalScreen);
            }
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {
                var p=new Vector3(x-.22f+col*.07f,1.81f-row*.065f,z-.184f);
                Box("Rubber keypad surround",p,new Vector3(.061f,.055f,.018f),steel);
                Box("Raised keypad button",p+Vector3.back*.014f,new Vector3(.043f,.037f,.019f),col==2&&row==2?terminalScreen:terminalPaint);
            }
            Shape("Rotary selector bezel",PrimitiveType.Cylinder,new Vector3(x+.19f,1.755f,z-.19f),new Vector3(.12f,.018f,.12f),steel,new Vector3(90,0,0));
            Shape("Knurled selector",PrimitiveType.Cylinder,new Vector3(x+.19f,1.755f,z-.22f),new Vector3(.087f,.025f,.087f),dark,new Vector3(90,0,0));
            Box("Selector index",new Vector3(x+.19f,1.78f,z-.248f),new Vector3(.009f,.028f,.006f),hull);
            for(int i=0;i<6;i++)Box("Lower cooling louvre",new Vector3(x,1.50f+i*.022f,z-.18f),new Vector3(.47f,.010f,.022f),steel);
            foreach(float dx in new[]{-.285f,.285f})foreach(float y in new[]{1.39f,2.21f})
                Shape("Captive face screw",PrimitiveType.Cylinder,new Vector3(x+dx,y,z-.178f),new Vector3(.024f,.006f,.024f),hull,new Vector3(90,0,0));
            foreach(float dx in new[]{-.18f,.18f})foreach(float dz in new[]{-.13f,.13f})Cylinder("Anchor bolt",new Vector3(x+dx,.12f,z+dz),.027f,.025f,rust);
            Beam("Conduit down rear",new Vector3(x+.22f,1.40f,z+.09f),new Vector3(x+.22f,.26f,z+.09f),.055f,dark);
            Beam("Conduit into foot",new Vector3(x+.22f,.26f,z+.09f),new Vector3(x+.12f,.11f,z+.09f),.055f,dark);
            foreach(float y in new[]{.40f,.95f,1.30f})Box("Cable retaining bracket",new Vector3(x+.22f,y,z+.09f),new Vector3(.09f,.035f,.08f),steel);
            Box("Inset identification plate",new Vector3(x,2.26f,z-.163f),new Vector3(.25f,.045f,.012f),steel);
        }
        void TerminalHousing(Vector3 center,Vector3 size,Material material)
        {
            float w=size.x/2,h=size.y/2,c=.055f;
            var outline=new[]{new Vector2(-w+c,-h),new Vector2(w-c,-h),new Vector2(w,-h+c),new Vector2(w,h-c),new Vector2(w-c,h),new Vector2(-w+c,h),new Vector2(-w,h-c),new Vector2(-w,-h+c)};
            var v=new Vector3[16];var triangles=new System.Collections.Generic.List<int>();
            for(int i=0;i<8;i++){v[i]=new Vector3(outline[i].x,outline[i].y,-size.z/2);v[i+8]=new Vector3(outline[i].x,outline[i].y,size.z/2);}
            for(int i=1;i<7;i++){triangles.AddRange(new[]{0,i+1,i,8,8+i,9+i});}
            for(int i=0;i<8;i++){int j=(i+1)%8;triangles.AddRange(new[]{i,j,i+8,j,j+8,i+8});}
            var mesh=new Mesh{name="Chamfered terminal enclosure"};mesh.vertices=v;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();meshes.Add(mesh);
            var obj=new GameObject("Chamfered service terminal");obj.transform.SetParent(section,false);obj.transform.localPosition=center;
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}

