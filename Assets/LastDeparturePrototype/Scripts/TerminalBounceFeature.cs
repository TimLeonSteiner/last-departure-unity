using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace YardSurvivalPrototype
{
    // Experimental, single-bounce screen-space GI. No history or baked-light feedback.
    public sealed class TerminalBounceFeature : ScriptableRendererFeature
    {
        public Shader bounceShader;
        [Range(0, 1)] public float strength = .35f;
        [Range(2, 16)] public float radius = 10;
        public static bool PreviewEnabled = true;
        Material material;
        BouncePass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (bounceShader == null) return;
            material = CoreUtils.CreateEngineMaterial(bounceShader);
            pass = new BouncePass(material);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var camera = data.cameraData.camera;
            if (RuntimeVisualBudget.Cheap || material == null || !PreviewEnabled || strength <= 0 ||
                data.cameraData.renderType != CameraRenderType.Base || camera.cameraType != CameraType.Game) return;
            Vector3 p = camera.transform.position;
            if (Mathf.Abs(p.x) > 24 || Mathf.Abs(p.z) > 24 || p.y < 0 || p.y > 8.5f) return;
            material.SetFloat("_BounceStrength", strength);
            material.SetFloat("_BounceRadius", radius);
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); }

        sealed class BouncePass : ScriptableRenderPass
        {
            readonly Material material;
            sealed class PassData
            {
                public Material material;
                public TextureHandle source, bounce;
                public int index;
            }
            public BouncePass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                requiresIntermediateTexture = true;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                desc.name = "Terminal bounce half resolution";
                desc.width = Mathf.Max(1, desc.width / 2);
                desc.height = Mathf.Max(1, desc.height / 2);
                desc.msaaSamples = MSAASamples.None;
                desc.depthBufferBits = DepthBits.None;
                desc.clearBuffer = false;
                var bounce = graph.CreateTexture(desc);
                AddPass(graph, resources, source, TextureHandle.nullHandle, bounce, 0, "Terminal GI trace");
                var full = graph.GetTextureDesc(source);
                full.name = "Terminal bounce composite";
                full.clearBuffer = false;
                var output = graph.CreateTexture(full);
                AddPass(graph, resources, source, bounce, output, 1, "Terminal GI bilateral composite");
                resources.cameraColor = output;
            }
            void AddPass(RenderGraph graph, UniversalResourceData resources, TextureHandle source,
                TextureHandle bounce, TextureHandle destination, int index, string label)
            {
                using (var builder = graph.AddRasterRenderPass<PassData>(label, out var data))
                {
                    data.material = material; data.source = source; data.bounce = bounce; data.index = index;
                    builder.UseTexture(source);
                    if (bounce.IsValid()) builder.UseTexture(bounce);
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.UseTexture(resources.cameraNormalsTexture);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                    {
                        var properties = new MaterialPropertyBlock();
                        properties.SetTexture("_BlitTexture", d.source);
                        if (d.bounce.IsValid()) properties.SetTexture("_BounceTexture", d.bounce);
                        properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.index,
                            MeshTopology.Triangles, 3, 1, properties);
                    });
                }
            }
        }
    }
}
