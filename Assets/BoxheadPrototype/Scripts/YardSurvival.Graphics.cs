using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        const string GraphicsKey = "Dustline.GraphicsPreset";
        static readonly string[] GraphicsNames = { "Low", "Medium", "High", "Fast" };
        static readonly int[] GraphicsDisplayOrder={3,0,1,2};
        int graphicsWidth,graphicsHeight;
        int graphicsPreset;
        float graphicsRainScale = 1;
        RenderPipelineAsset previousQualityPipeline;
        UniversalRenderPipelineAsset graphicsPipeline;
        float originalShadowDistance;
        int originalMsaa;

        void InitializeGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            previousQualityPipeline = QualitySettings.renderPipeline;
            var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (source != null)
            {
                // Change a runtime copy, never the project's shared pipeline asset.
                graphicsPipeline = Instantiate(source);
                originalShadowDistance = source.shadowDistance;
                originalMsaa = source.msaaSampleCount;
                QualitySettings.renderPipeline = graphicsPipeline;
            }
            gameObject.AddComponent<RuntimeVisualBudget>();
            ApplyGraphicsPreset(PlayerPrefs.GetInt(GraphicsKey, Application.platform == RuntimePlatform.WebGLPlayer ? 0 : 2), false);
        }

        void ApplyGraphicsPreset(int preset, bool save)
        {
            graphicsPreset = Mathf.Clamp(preset, 0, 3);
            bool low = graphicsPreset == 0 || graphicsPreset == 3;
            bool minimum=graphicsPreset==3;
            RuntimeVisualBudget.Set(graphicsPreset);
            graphicsRainScale = minimum ? .05f : low ? .2f : graphicsPreset == 1 ? .5f : 1;
            if (graphicsPipeline != null)
            {
                UpdateGraphicsResolution();
                graphicsPipeline.shadowDistance = low ? 0 : graphicsPreset == 1 ? Mathf.Min(25, originalShadowDistance) : originalShadowDistance;
                graphicsPipeline.msaaSampleCount = low ? 1 : originalMsaa;
            }
            if (arenaCamera != null)
            {
                var data = arenaCamera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = !low;
                data.renderShadows = !low;
            }
            foreach (var particles in rainfall)
            {
                var collision = particles.collision;
                collision.enabled = !low && particles.name == "Slanted rainfall";
                particles.Clear();
            }
            appliedRainIntensity = -1;
            UpdateWeather();
            if (save) {
                PlayerPrefs.SetInt(GraphicsKey, graphicsPreset);PlayerPrefs.Save();
            }
        }

        public static float GraphicsScale(int preset,int width,int height)
        {
            float scale=preset==3?.35f:preset==0?.5f:preset==1?.75f:1;
            float maxWidth=preset==3?640:preset==0?960:preset==1?1440:1920;
            return Mathf.Clamp(Mathf.Min(scale,maxWidth/Mathf.Max(1,width),maxWidth*.5625f/Mathf.Max(1,height)),.1f,1);
        }
        void UpdateGraphicsResolution()
        {
            graphicsWidth=Screen.width;graphicsHeight=Screen.height;
            if (graphicsPipeline != null)
            {
                // The wrist canvas is rendered by the world camera. Keep its
                // lettering at output resolution while the menu owns the pause.
                graphicsPipeline.renderScale = HologramBlocking
                    ? 1f
                    : GraphicsScale(graphicsPreset, graphicsWidth, graphicsHeight);
            }
        }
        void TickGraphicsResolution()
        {
            if(graphicsPipeline!=null && (graphicsWidth!=Screen.width || graphicsHeight!=Screen.height))
                UpdateGraphicsResolution();
        }

        void DisposeGraphics()
        {
            if (graphicsPipeline == null) return;
            if (QualitySettings.renderPipeline == graphicsPipeline) QualitySettings.renderPipeline = previousQualityPipeline;
            Destroy(graphicsPipeline);
        }
    }
}
