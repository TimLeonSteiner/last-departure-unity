using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YardSurvivalPrototype
{
    // One shared planar capture for the floor, bounded in resolution and refresh rate.
    public sealed class PuddleReflections : MonoBehaviour
    {
        Camera reflectionCamera;
        RenderTexture reflection;
        Material water;
        Renderer[] puddles;
        bool[] enabledStates;
        float nextCapture;
        public void Initialize(Material material)
        {
            water=material;
            // Browser clients keep the lit water shader without a second scene render.
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                water.SetFloat("_ReflectionReady",0);
                enabled=false;
                return;
            }
            puddles=System.Array.FindAll(GetComponentsInChildren<Renderer>(),r=>r.sharedMaterial==water);
            enabledStates=new bool[puddles.Length];
            reflection=new RenderTexture(640,360,24,RenderTextureFormat.ARGBHalf)
            {name="Puddle room reflection",useMipMap=true,autoGenerateMips=true};
            reflection.Create();
            var obj=new GameObject("Puddle reflection camera");obj.transform.SetParent(transform,false);
            reflectionCamera=obj.AddComponent<Camera>();reflectionCamera.enabled=false;
            var data=reflectionCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=false;data.renderShadows=false;
            data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
            water.SetTexture("_PlanarReflection",reflection);
        }
        void LateUpdate()
        {
            // Inventory can move its camera while gameplay is paused. Use the lit
            // water fallback instead of periodically rendering the entire room again.
            if(RuntimeVisualBudget.Cheap || Time.timeScale <= 0){if(water!=null)water.SetFloat("_ReflectionReady",0);return;}
            if(Time.unscaledTime<nextCapture)return;
            Capture(Camera.main);
            nextCapture=Time.unscaledTime+.05f;
        }
        public void Capture(Camera source)
        {
            if(source==null || !source.isActiveAndEnabled || reflectionCamera==null || !reflection.IsCreated())return;
            const float height=.04f;
            var mirror=Matrix4x4.identity;mirror.m11=-1;mirror.m13=2*height;
            reflectionCamera.CopyFrom(source);reflectionCamera.enabled=false;
            reflectionCamera.targetTexture=reflection;
            reflectionCamera.cullingMask=source.cullingMask & ~(1<<30);
            reflectionCamera.useOcclusionCulling=false;
            reflectionCamera.transform.position=mirror.MultiplyPoint(source.transform.position);
            reflectionCamera.transform.rotation=Quaternion.LookRotation(mirror.MultiplyVector(source.transform.forward),mirror.MultiplyVector(source.transform.up));
            reflectionCamera.worldToCameraMatrix=source.worldToCameraMatrix*mirror;
            var view=reflectionCamera.worldToCameraMatrix;
            var point=view.MultiplyPoint(new Vector3(0,height+.005f,0));
            var normal=view.MultiplyVector(Vector3.up).normalized;
            var clip=new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal));
            reflectionCamera.projectionMatrix=reflectionCamera.CalculateObliqueMatrix(clip);
            bool inverted=GL.invertCulling;
            try
            {
                for(int i=0;i<puddles.Length;i++)if(puddles[i]!=null){enabledStates[i]=puddles[i].enabled;puddles[i].enabled=false;}
                GL.invertCulling=!inverted;
                RenderPipeline.SubmitRenderRequest(reflectionCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=reflection});
                water.SetMatrix("_ReflectionVP",GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix,true)*view);
                water.SetFloat("_ReflectionReady",1);
            }
            finally
            {
                GL.invertCulling=inverted;
                for(int i=0;i<puddles.Length;i++)if(puddles[i]!=null)puddles[i].enabled=enabledStates[i];
            }
        }
        void OnDestroy()
        {
            if(reflection!=null){reflection.Release();Destroy(reflection);}
            if(reflectionCamera!=null)Destroy(reflectionCamera.gameObject);
        }
    }
}
