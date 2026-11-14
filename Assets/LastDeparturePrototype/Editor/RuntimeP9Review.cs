using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using YardSurvivalPrototype;
[InitializeOnLoad] public static class RuntimeP9Review {
const string Request="Assets/LastDeparturePrototype/P9Runtime.request",Root="Assets/LastDeparturePrototype/P9RuntimeReview";
const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
static P9ReviewInput driver; static YardSurvival game; static double at; static int weaponIndex; static readonly int[] Weapons={2,12,0,15}; static int stage; static bool active;
static RuntimeP9Review(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("D27.P9Live",false)){SessionState.SetBool("D27.P9Background",Application.runInBackground);Application.runInBackground=true;EditorApplication.isPaused=false;SessionState.SetBool("D27.P9Ready",true);at=EditorApplication.timeSinceStartup+3;}};}
static object Get(string name)=>typeof(YardSurvival).GetField(name,F).GetValue(game);
static void Set(string name,object value)=>typeof(YardSurvival).GetField(name,F).SetValue(game,value);
static void Call(string name)=>typeof(YardSurvival).GetMethod(name,F).Invoke(game,null);
static void Poll(){
if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
if(File.Exists(Request)){File.Delete(Request);if(EditorApplication.isPlaying)throw new Exception("P9 review requires stopped editor");Directory.CreateDirectory(Root);SessionState.SetBool("D27.P9Ready",false);SessionState.SetBool("D27.P9Live",true);EditorSettings.enterPlayModeOptionsEnabled=false;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;return;}
if(!active&&SessionState.GetBool("D27.P9Ready",false)&&EditorApplication.isPlaying&&EditorApplication.timeSinceStartup>=at){game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;SessionState.SetBool("D27.P9Live",false);SessionState.SetBool("D27.P9Ready",false);active=true;weaponIndex=0;stage=0;at=EditorApplication.timeSinceStartup+3;var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(t).Focus();return;}
if(!active)return;EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();if(!EditorApplication.isPlaying){active=false;return;}
try{
if(stage>0){Set("remainingToSpawn",0);Set("nextWaveCountdown",-1f);Set("health",100);if(driver!=null)driver.hold=stage>=2;Cursor.lockState=CursorLockMode.Locked;if(Mouse.current!=null)InputSystem.QueueStateEvent(Mouse.current,new MouseState{buttons=(ushort)(stage>=2?2:0)});}
if(EditorApplication.timeSinceStartup<at)return;
switch(stage){
case 0: Call("PlayFromMenu");driver=new GameObject("Editor P9 input check").AddComponent<P9ReviewInput>();Set("remainingToSpawn",0);stage=1;at=EditorApplication.timeSinceStartup+1;break;
case 1: Capture("Hip");stage=2;at=EditorApplication.timeSinceStartup+2;break;
case 2: Capture("ADS");if((float)Get("aimBlend")<.99f)throw new Exception("Normal ADS input did not reach full aim; binding/focus needs manual review");Set("ammunition",WeaponCatalog.All[Weapons[weaponIndex]].capacity-2);Call("BeginReload");stage=3;at=EditorApplication.timeSinceStartup+WeaponCatalog.All[Weapons[weaponIndex]].reload+2;break;
case 3: Capture("ADS-AfterReload");if((bool)Get("reloading")||(float)Get("aimBlend")<.99f)throw new Exception("Reload/ADS did not finish");File.AppendAllText(Root+"/Result.txt","COMPLETE normal gameplay update: hip, ADS, reload, ADS again. Visual review required.\n");if(++weaponIndex<Weapons.Length){typeof(YardSurvival).GetMethod("EquipWeapon",F).Invoke(game,new object[]{Weapons[weaponIndex],WeaponCatalog.All[Weapons[weaponIndex]].capacity,-1});stage=1;at=EditorApplication.timeSinceStartup+1;}else{active=false;InputSystem.QueueStateEvent(Mouse.current,new MouseState());Cursor.lockState=CursorLockMode.None;Application.runInBackground=SessionState.GetBool("D27.P9Background",false);EditorApplication.isPlaying=false;}break;
}
}catch(Exception e){File.AppendAllText(Root+"/Result.txt","FAIL "+e+"\n");active=false;Application.runInBackground=SessionState.GetBool("D27.P9Background",false);EditorApplication.isPlaying=false;}
}
static void Capture(string name){name=Weapons[weaponIndex].ToString("00")+"-"+name;ScreenCapture.CaptureScreenshot(Root+"/"+name+"-Game.png");var cam=(Camera)Get("arenaCamera");var view=(ViewWeapon)Get("viewWeapon");File.AppendAllText(Root+"/Result.txt",DateTime.Now+" "+name+" weapon="+Get("weaponId")+" aim="+Get("aimBlend")+" focused="+Application.isFocused+" frame="+Time.frameCount+" paused="+EditorApplication.isPaused+" enabled="+game.enabled+" timescale="+Time.timeScale+" cursor="+Cursor.lockState+" mouse="+Mouse.current.rightButton.isPressed+" screen="+Get("frontScreen")+" local="+view.transform.localPosition+"\n");var rt=new RenderTexture(1200,750,24);rt.Create();var tex=new Texture2D(1200,750,TextureFormat.RGB24,false);var prev=RenderTexture.active;try{RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,750),0,0);tex.Apply();File.WriteAllBytes(Root+"/"+name+".png",tex.EncodeToPNG());}finally{RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}}
}

[DefaultExecutionOrder(-32000)] public class P9ReviewInput:MonoBehaviour {public bool hold;void Update(){if(Mouse.current!=null)InputState.Change(Mouse.current.rightButton,hold?1f:0f);Cursor.lockState=CursorLockMode.Locked;}}
