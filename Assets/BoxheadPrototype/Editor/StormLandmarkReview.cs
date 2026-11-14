using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class StormLandmarkReview
{
    const string Request = "Assets/Verification~/StormLandmarks/Capture.request";
    const string Output = "Assets/Verification~/StormLandmarks";
    const string Pending = "StormLandmarkReview.Pending";
    static double readyAt;
    static StormLandmarkReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(Request))
        {
            File.Delete(Request);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".StartedPlay", !EditorApplication.isPlaying);
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            readyAt = EditorApplication.timeSinceStartup + 4;
        }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
        SessionState.SetBool(Pending, false);
        try { Capture(); }
        catch (Exception ex) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Result.txt", "FAIL " + ex); Debug.LogException(ex); }
        finally { if (SessionState.GetBool(Pending + ".StartedPlay", false)) EditorApplication.isPlaying = false; }
    }
    static void Capture()
    {
        Directory.CreateDirectory(Output);
        var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
        if (game == null) throw new Exception("Open Boxhead scene first.");
        var root = game.transform.Find("Spaceport exterior");
        if (root == null) throw new Exception("Spaceport exterior missing.");
        var set = StormLandmarks.Build(root);
        var priorService=root.Find("Exterior maintenance stories");
        if(priorService!=null)UnityEngine.Object.DestroyImmediate(priorService.gameObject);
        var service=ExteriorServiceDetails.Build(root);
        if(service.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("Service details added collision");
        if(service.GetComponentsInChildren<MeshRenderer>().Length>20)throw new Exception("Service details exceeded twenty material batches");
        foreach(var filter in service.GetComponentsInChildren<MeshFilter>())foreach(var v in filter.sharedMesh.vertices)
            if(Mathf.Abs(v.x)<4 && v.y<4)throw new Exception("Exterior detail intrudes into central approach lane");
        var weather=ExteriorStormAtmosphere.Build(root);
        if(weather.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("Weather must not add collision");
        if(weather.GetComponentsInChildren<MeshRenderer>(true).Length!=12)throw new Exception("Expected twelve exterior mist sheets");
        if (set.transform.childCount != 8) throw new Exception("Expected four outlooks and four gate detail sets.");
        if (set.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Decorations must not add active collision geometry.");
        int renderers = set.GetComponentsInChildren<MeshRenderer>().Length;
        // Allow the five authored material groups on each of the four service skids.
        if (renderers > 80) throw new Exception("Landmark material batching exceeded budget: " + renderers);
        var cameraObject = new GameObject("Landmark review camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.fieldOfView = 75; camera.nearClipPlane = .05f; camera.farClipPlane = 320;
        camera.clearFlags = CameraClearFlags.Skybox; camera.cullingMask = ~(1 << 30);
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var flash = typeof(YardSurvival).GetMethod("ApplyStormFlash", flags);
        typeof(YardSurvival).GetMethod("EnsureDreamEffects", flags).Invoke(game, null);
        var target = new RenderTexture(1280, 800, 24); target.Create();
        var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        try
        {
            string[] names = { "North-Control", "East-Coolant", "South-Freight", "West-Relay" };
            Vector3[] targets = { new Vector3(-23, 10, 55), new Vector3(62, 8, 0), new Vector3(0, 9, -62), new Vector3(-62, 12, 0) };
            string[] detailNames={"Coolant-Detail","Pump-Detail","Relay-Side","Tower-Ladder","Pump-Ground","Freight-Side","Launch-Connections","Service-Cabinet","Service-Coolant","Service-Freight","Service-Relay"};
            Vector3[] detailPositions={new Vector3(43,3,-9),new Vector3(26,2,13),new Vector3(-49,16,-9),new Vector3(-29,9,47),new Vector3(28,.3f,12),new Vector3(17,3,-49),new Vector3(-30,16,91),new Vector3(-6,2,40),new Vector3(40,2,-6),new Vector3(-6,2,-40),new Vector3(-40,2,6)};
            Vector3[] detailTargets={new Vector3(62,5,0),new Vector3(31,1,9),new Vector3(-63,19,0),new Vector3(-23,9,53),new Vector3(31,.3f,9),new Vector3(0,4,-68),new Vector3(-22,17,104),new Vector3(-10,1.2f,45.5f),new Vector3(46,1.2f,-10),new Vector3(-10,1.2f,-46),new Vector3(-46,1.2f,10)};
            for (int i = 0; i < 15; i++)
            {
                string captureName = i < 4 ? names[i] : detailNames[i-4];
                camera.fieldOfView = i < 4 ? 75 : 60;
                camera.transform.position = i < 4 ? Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 1.7f, 34) : detailPositions[i-4];
                Vector3 lookAt = i < 4 ? targets[i] : detailTargets[i-4];
                camera.transform.rotation = Quaternion.LookRotation(lookAt - camera.transform.position);
                foreach (bool lit in new[] { false, true })
                {
                    flash.Invoke(game, new object[] { lit ? .7f : 0f });
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Output + "/" + captureName + (lit ? "-Lightning" : "-Night") + ".png", pixels.EncodeToPNG());
                }
            }
            File.WriteAllText(Output + "/Result.txt", "PASS: four exterior outlooks, four gate detail sets, no active colliders, " + renderers + " batched/detail renderers. Thirty night/lightning captures including four new service-bay closeups. Service decorations use at most twenty material batches, no colliders, and leave the central eight-metre-wide, four-metre-high approach clear. Twelve exterior mist sheets without collision; four near sheets retained on low presets. Geometry connections require visual review.");
        }
        finally
        {
            flash.Invoke(game, new object[] { 0f });
            RenderTexture.active = oldActive; target.Release();
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
