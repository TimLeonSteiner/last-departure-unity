using System;using System.IO;using System.Reflection;using UnityEditor;using UnityEngine;using UnityEngine.Rendering;using YardSurvivalPrototype;
[InitializeOnLoad]public static class BoltExplosionReview{
static int stage;static double at,shotAt;static YardSurvival game;static bool background;
static BoltExplosionReview(){EditorApplication.update+=Poll;}
static void Poll(){if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
const string root="Assets/BoxheadPrototype/BoltExplosionReview/";
try{
if(stage==0&&File.Exists("Assets/BoxheadPrototype/BoltExplosionReview.request")){
File.Delete("Assets/BoxheadPrototype/BoltExplosionReview.request");game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();background=Application.runInBackground;Application.runInBackground=true;
typeof(YardSurvival).GetMethod("PlayFromMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);typeof(YardSurvival).GetField("remainingToSpawn",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,0);
typeof(YardSurvival).GetMethod("ReleaseMouse",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);game.CoopQAEquip("7");game.CoopQAView("0,20");stage=1;at=EditorApplication.timeSinceStartup+.8;Directory.CreateDirectory(root);return;}
if(stage==0)return;EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();if(EditorApplication.timeSinceStartup<at)return;
if(stage==1){game.CoopQAAction("fire");stage=2;at=EditorApplication.timeSinceStartup+.02;return;}
if(stage==2){var explosion=UnityEngine.Object.FindAnyObjectByType<ArsenalExplosionVisual>();if(explosion==null){at=EditorApplication.timeSinceStartup+.02;return;}shotAt=EditorApplication.timeSinceStartup;stage=3;at=shotAt+.04;return;}
float[] times={.04f,.25f,.8f,1.8f,3f};int index=stage-3;var camera=(Camera)typeof(YardSurvival).GetField("arenaCamera",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
var target=new RenderTexture(1280,800,24);target.Create();var image=new Texture2D(1280,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
try{RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(root+"Stage-"+index+".png",image.EncodeToPNG());}
finally{RenderTexture.active=old;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
if(index==times.Length-1){File.WriteAllText(root+"Result.txt","COMPLETE "+DateTime.Now.ToString("s")+": actual fired explosive crossbow projectile, real runtime blast captured at5ages. Inspect fire/sparks/debris/smoke/dust/ring. No damage or effect simulation substituted.");game.ResetRun();Application.runInBackground=background;stage=0;}else{stage++;at=shotAt+times[index+1];}
}catch(Exception e){Directory.CreateDirectory(root);File.WriteAllText(root+"Result.txt","FAIL "+e);stage=0;Debug.LogException(e);}}
}
