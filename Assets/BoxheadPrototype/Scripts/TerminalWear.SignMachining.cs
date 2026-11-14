using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
namespace YardSurvivalPrototype
{
    public sealed partial class TerminalWear
    {
        void SignMachining(int side,Vector3 origin,Quaternion rotation)
        {
            Vector3 P(Vector3 p)=>origin+rotation*p;
            void B(Vector3 p,Vector3 size,Material mat)=>Box(P(p),size,mat,rotation);
            void Tube(Vector3 a,Vector3 b,float radius,Material mat,int sides=12)
            {
                var axis=(b-a).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(axis,u);
                var verts=new List<Vector3>();var tris=new List<int>();
                for(int end=0;end<2;end++)for(int i=0;i<sides;i++){float angle=i*Mathf.PI*2/sides;verts.Add((end==0?a:b)+(u*Mathf.Cos(angle)+v*Mathf.Sin(angle))*radius);}
                for(int i=0;i<sides;i++){int j=(i+1)%sides;tris.AddRange(new[]{i,j,sides+j,i,sides+j,sides+i});}
                for(int i=1;i<sides-1;i++)tris.AddRange(new[]{0,i+1,i,sides,sides+i,sides+i+1});
                var mesh=new Mesh{name="Machined sign fitting"};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
                Add(mesh,mat,Matrix4x4.TRS(origin,rotation,Vector3.one));
            }
            void Screw(Vector3 p,float radius=.035f)
            {
                Tube(p+Vector3.forward*.008f,p-Vector3.forward*.016f,radius*1.45f,ink,16);
                Tube(p,p-Vector3.forward*.035f,radius,steel,6);
                B(p-Vector3.forward*.037f,new Vector3(radius*1.25f,.008f,.004f),ink);
            }
            void Plate(Vector3 p,Vector2 size,Material material)
            {
                float x=size.x/2,y=size.y/2,c=Mathf.Min(.07f,y*.25f);
                var edge=new[]{new Vector3(-x+c,-y,0),new Vector3(x-c,-y,0),new Vector3(x,-y+c,0),new Vector3(x,y-c,0),new Vector3(x-c,y,0),new Vector3(-x+c,y,0),new Vector3(-x,y-c,0),new Vector3(-x,-y+c,0)};
                var verts=new List<Vector3>();var tris=new List<int>();foreach(float z in new[]{0f,.035f})foreach(var q in edge)verts.Add(p+q+Vector3.forward*z);
                for(int i=1;i<7;i++)tris.AddRange(new[]{0,i+1,i,8,8+i,8+i+1});
                for(int i=0;i<8;i++){int j=(i+1)%8;tris.AddRange(new[]{i,j,8+j,i,8+j,8+i});}
                var mesh=new Mesh{name="Chamfered equipment plate"};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);Add(mesh,material,Matrix4x4.TRS(origin,rotation,Vector3.one));
            }
            void Label(string words,Vector3 p,Vector2 size,float fontSize)
            {
                var obj=new GameObject("Engraved plate / "+words);obj.transform.SetParent(transform,false);obj.transform.localPosition=P(p);obj.transform.localRotation=rotation;
                var text=obj.AddComponent<TextMeshPro>();text.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");text.text=words;text.color=new Color(.73f,.76f,.73f);text.fontSize=fontSize;text.enableAutoSizing=true;text.fontSizeMin=fontSize*.7f;text.fontSizeMax=fontSize;text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=size;text.overflowMode=TextOverflowModes.Truncate;
                var mat=text.fontMaterial;if(mat.HasProperty("_ZTestMode"))mat.SetFloat("_ZTestMode",4);ownedMaterials.Add(mat);text.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
            // Rolled perimeter tubes catch the practical lights; fixings have washers and hex heads.
            foreach(float y in new[]{-.80f,.86f})Tube(new Vector3(-4.37f,y,-.43f),new Vector3(4.37f,y,-.43f),.025f,steel);
            foreach(float x in new[]{-4.37f,4.37f})Tube(new Vector3(x,-.80f,-.43f),new Vector3(x,.86f,-.43f),.025f,steel);
            foreach(float x in new[]{-4.12f,-2.1f,0,2.1f,4.12f})foreach(float y in new[]{-.66f,.72f})Screw(new Vector3(x,y,-.43f));
            // Three articulated hinge knuckles on each service edge.
            foreach(float x in new[]{-4.4f,4.4f})foreach(float y in new[]{-.42f,.35f})
            {
                B(new Vector3(x,y,-.44f),new Vector3(.24f,.30f,.045f),steel);
                for(int i=-1;i<=1;i++)Tube(new Vector3(x,y+i*.085f-.034f,-.49f),new Vector3(x,y+i*.085f+.034f,-.49f),.047f,i==0?ink:steel);
                Screw(new Vector3(x-.075f,y,-.48f),.02f);Screw(new Vector3(x+.075f,y,-.48f),.02f);
            }
            Plate(new Vector3(0,-1.10f,-.42f),new Vector2(4.4f,.44f),ink);
            Label("D27 / SECTOR "+(side+1).ToString("00")+"    •    AUTHORIZED PERSONNEL",new Vector3(0,-1.02f,-.43f),new Vector2(4.05f,.17f),1.15f);
            Label("ASSET 27–10"+side+"   |   INSPECTED 06 / 24   |   230 V",new Vector3(.25f,-1.19f,-.43f),new Vector2(3.45f,.12f),.8f);
            for(int i=0;i<24;i++)B(new Vector3(-1.94f+i*.018f,-1.19f,-.438f),new Vector3(i%3==0?.012f:.006f,.10f,.004f),ivory);
            foreach(float x in new[]{-2.08f,2.08f})Screw(new Vector3(x,-1.10f,-.44f),.024f);
            // Junction enclosure: folded lip, separate door, recessed vents, latch and cable glands.
            B(new Vector3(4.88f,-.19f,-.32f),new Vector3(.67f,1.07f,.24f),ink);
            Plate(new Vector3(4.88f,-.19f,-.46f),new Vector2(.61f,1.01f),steel);
            for(int i=0;i<7;i++)
            {
                B(new Vector3(4.88f,-.37f+i*.055f,-.472f),new Vector3(.37f,.025f,.008f),ink);
                Box(P(new Vector3(4.88f,-.36f+i*.055f,-.482f)),new Vector3(.37f,.015f,.028f),steel,rotation*Quaternion.Euler(22,0,0));
            }
            Label("LOCAL\nISOLATOR",new Vector3(4.88f,.16f,-.476f),new Vector2(.48f,.23f),.85f);
            Tube(new Vector3(5.08f,-.14f,-.47f),new Vector3(5.08f,-.14f,-.52f),.034f,ivory,6);
            foreach(float y in new[]{-.61f,.24f})Screw(new Vector3(4.65f,y,-.48f),.019f);
            for(int k=0;k<2;k++)
            {
                float x=4.75f+k*.22f;Tube(new Vector3(x,-.73f,-.31f),new Vector3(x,-.84f,-.31f),.052f,steel,6);
                for(int i=0;i<18;i++){float t=i/17f,t2=(i+1)/17f;Tube(new Vector3(x+.18f*Mathf.Sin(t*Mathf.PI),-.82f-t*.75f,-.31f),new Vector3(x+.18f*Mathf.Sin(t2*Mathf.PI),-.82f-t2*.75f,-.31f),.026f,ink);}
            }
            // Thin grille ribs and protective end caps make the lamp housings readable up close.
            foreach(float x in new[]{-2.6f,2.6f})
            {
                for(int i=-3;i<=3;i++)B(new Vector3(x+i*.09f,.925f,-.76f),new Vector3(.012f,.025f,.22f),steel);
                foreach(float end in new[]{-.39f,.39f}){B(new Vector3(x+end,1.0f,-.76f),new Vector3(.035f,.18f,.31f),steel);Screw(new Vector3(x+end,.99f,-.92f),.02f);}
            }
        }
    }
}
