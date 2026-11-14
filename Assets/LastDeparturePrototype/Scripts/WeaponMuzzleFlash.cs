using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YardSurvivalPrototype
{
    public sealed class WeaponMuzzleFlash : MonoBehaviour
    {
        Mesh mesh;
        Material material;
        MeshRenderer flashRenderer;
        Light flashLight;
        float remaining;
        int firedFrame = -1;
        int shot;

        public void Initialize()
        {
            // Feathered flame silhouette with a hot centre, rather than solid geometry.
            const int segments = 48;
            var vertices = new Vector3[1 + segments * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 9];
            colors[0] = new Color(5, 4.2f, 3, 1);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                vertices[1+i] = direction * .018f;
                colors[1+i] = new Color(3, 1.5f, .4f, .8f);
                float radius = .073f*(.75f+.18f*Mathf.Sin(angle*5+.7f)+.16f*Mathf.Sin(angle*9)+.1f*Mathf.Cos(angle*13));
                vertices[1+segments+i] = direction * radius;
                colors[1+segments+i] = new Color(1, .22f, .015f, 0);
                int next=(i+1)%segments, t=i*9;
                triangles[t]=0;triangles[t+1]=1+i;triangles[t+2]=1+next;
                triangles[t+3]=1+i;triangles[t+4]=1+segments+i;triangles[t+5]=1+segments+next;
                triangles[t+6]=1+i;triangles[t+7]=1+segments+next;triangles[t+8]=1+next;
            }
            mesh=new Mesh{name="Muzzle flame"};mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            flashRenderer=gameObject.AddComponent<MeshRenderer>();
            material=new Material(Resources.Load<Shader>("MuzzleFlash"));
            flashRenderer.sharedMaterial=material;flashRenderer.shadowCastingMode=ShadowCastingMode.Off;
            flashRenderer.receiveShadows=false;flashRenderer.renderingLayerMask=2;flashRenderer.enabled=false;
            flashLight=gameObject.AddComponent<Light>();flashLight.type=LightType.Point;
            flashLight.color=new Color(1,.57f,.2f);flashLight.range=2.5f;flashLight.shadows=LightShadows.None;
            flashLight.GetUniversalAdditionalLightData().renderingLayers=3;flashLight.enabled=false;
            gameObject.layer=30;
        }
        public void Fire()
        {
            remaining=.055f;firedFrame=Time.frameCount;shot++;
            transform.localRotation=Quaternion.Euler(0,0,(shot*137.5f)%360);
            transform.localScale=Vector3.one*(.85f+(shot%3)*.12f);
            flashRenderer.enabled=true;flashLight.enabled=true;flashLight.intensity=1;
        }
        public void Tick(float dt, bool hidden)
        {
            // Guarantee a visible frame even when the shot lands on a slow frame.
            if(Time.frameCount!=firedFrame)remaining=Mathf.Max(0,remaining-dt);
            flashRenderer.enabled=flashLight.enabled=!hidden&&remaining>0;
            flashLight.intensity=Mathf.Clamp01(remaining/.055f);
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
