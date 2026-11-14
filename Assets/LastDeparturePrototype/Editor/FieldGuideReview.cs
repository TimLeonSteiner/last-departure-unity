using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class FieldGuideReview
{
    const string Root="Assets/Verification~/";
    const string StageKey="PreparationReview.Stage";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static double next;
    static YardSurvival game;
    static int beforeScrap;
    static FieldGuideReview(){EditorApplication.update+=Tick;}
    static object Read(string name)=>typeof(YardSurvival).GetField(name,Flags).GetValue(game);
    static void Set(string name,object value)=>typeof(YardSurvival).GetField(name,Flags).SetValue(game,value);
    static object Call(string name,params object[] args)=>typeof(YardSurvival).GetMethod(name,Flags).Invoke(game,args);
    static string Step=>Read("preparationStep").ToString();
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);File.AppendAllText(Root+"PreparationChecks.txt","PASS "+label+"\n");}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Root+"PreparationReview.request"))
        {
            File.Delete(Root+"PreparationReview.request");File.WriteAllText(Root+"PreparationChecks.txt","");
            SessionState.SetInt(StageKey,1);next=EditorApplication.timeSinceStartup+6;
            if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
        }
        int stage=SessionState.GetInt(StageKey,0);
        if(stage==0||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if(EditorApplication.timeSinceStartup<next)return;
        try
        {
            game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
            if(stage==1)
            {
                Call("RestoreLaunchRendering");Call("PlayFromMenu");
                Check(Step=="None","No tutorial before first wave clear");
                var prefab=Resources.Load<GameObject>("Construction/Barricade");
                Check(prefab!=null && prefab.transform.childCount==5,"Meshy barricade prefab with damage sections loaded");
                var preview=UnityEngine.Object.Instantiate(prefab,new Vector3(200,2,200),Quaternion.identity);
                var lamp=new GameObject("Barricade review light").AddComponent<Light>();lamp.type=LightType.Point;lamp.range=12;lamp.intensity=12;lamp.transform.position=new Vector3(198,5,197);
                try
                {
                    int triangles=0;foreach(var f in preview.GetComponentsInChildren<MeshFilter>())triangles+=f.sharedMesh.triangles.Length/3;
                    Check(triangles<=12000,"Barricade gameplay mesh budget");
                    SpaceportAssetBuild.Capture(new Vector3(202.5f,3.4f,196.4f),preview.transform.position,"MeshyBarricade");
                    EntranceBoards.TrackImported(preview,game);var damage=preview.GetComponent<EntranceBoards>();damage.SetHealth(40);
                    Check(!preview.transform.Find("Damage 4").gameObject.activeSelf,"Meshy damage sections break away");
                    damage.SetHealth(100);Check(preview.transform.Find("Damage 4").gameObject.activeSelf,"Barricade repairs restore geometry");
                }
                finally{UnityEngine.Object.Destroy(preview);UnityEngine.Object.Destroy(lamp.gameObject);}

                var enemies=(IList)Read("enemies");
                foreach(var enemy in enemies){var body=(Transform)enemy.GetType().GetField("body").GetValue(enemy);UnityEngine.Object.Destroy(body.gameObject);}
                enemies.Clear();Set("remainingToSpawn",0);
                Call("CompleteWave");
                Check(Step=="OpenLink","First wave completion triggers keyboard");
                float timer=(float)Read("nextWaveCountdown");Call("TickNextWave",60f);
                Check((float)Read("nextWaveCountdown")==timer && !(bool)Read("fighting"),"Countdown held during lesson");
                var bindings=(Array)Read("bindings");var binding=bindings.GetValue(23);
                var primary=binding.GetType().GetField("primary");int old=(int)primary.GetValue(binding);
                var method=typeof(YardSurvival).GetMethod("PreparationBinding",Flags);var action=Enum.ToObject(method.GetParameters()[0].ParameterType,23);
                try{primary.SetValue(binding,(int)Key.F12);Check((int)method.Invoke(game,new[]{action})==(int)Key.F12,"Highlight uses remapped field-link key");}
                finally{primary.SetValue(binding,old);}
            }
            else if(stage==2)ScreenCapture.CaptureScreenshot(Root+"Preparation-Keyboard.png");
            else if(stage==3)
            {
                var oldKeyboard=Keyboard.current;
                var testKeyboard=InputSystem.AddDevice<Keyboard>();testKeyboard.MakeCurrent();
                var oldBackground=InputSystem.settings.backgroundBehavior;
                var oldMode=InputSystem.settings.updateMode;
                try
                {
                    InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                    _=testKeyboard.tabKey.wasPressedThisFrame;
                    var update=typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null);
                    update.Invoke(null,new object[]{InputUpdateType.Dynamic});
                    InputState.Change(testKeyboard,new KeyboardState(Key.Tab),InputUpdateType.Dynamic);
                    Call("TickHologram");
                    Check((bool)Read("hologramOpen"),"Tab input opens the field link");
                }
                finally{InputSystem.RemoveDevice(testKeyboard);InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.updateMode=oldMode;if(oldKeyboard!=null)oldKeyboard.MakeCurrent();}
                Check(Step=="SelectBlueprint" && Time.timeScale==0,"Field link advances to selection and pauses");
            }
            else if(stage==4)ScreenCapture.CaptureScreenshot(Root+"Preparation-Select.png");
            else if(stage==5)
            {
                beforeScrap=(int)Read("supplies");Call("SelectDefense",1);Call("CloseHologram");
                Check(Step=="PlaceBlueprint" && (int)Read("supplies")==beforeScrap && (bool)Read("buildFireBlocked"),"Turret selection free; click-through blocked");
                Call("RestoreHologram");
                Check(Time.timeScale==1,"Simulation restored for placement");
                Check(!(bool)Call("TryPlaceDefense",new Vector3(1000,0,1000),1,false) && Step=="PlaceBlueprint","Invalid placement does not advance");
                Vector3 position=Vector3.zero;bool placed=false;
                for(int z=-7;z<=7&&!placed;z+=2)for(int x=-7;x<=7&&!placed;x+=2)
                {position=new Vector3(x,0,z);placed=(bool)Call("TryPlaceDefense",position,1,false);}
                Check(placed && Step=="BuildBlueprint","Successful placement advances to building");
                var turrets=(IList)Read("turrets");var turret=turrets[turrets.Count-1];
                int id=(int)turret.GetType().GetField("netId").GetValue(turret);
                bool completed=false;
                for(int direction=0;direction<4&&!completed;direction++)
                {
                    Vector3 collector=position+Quaternion.Euler(0,direction*90,0)*new Vector3(0,.65f,1.8f);
                    for(int hit=0;hit<6;hit++)game.ApplyHammerHit(id,collector);
                    Call("TickPreparationTutorial");completed=Step=="None";
                }
                Check(completed,"Only completed construction finishes lesson");
                Check((float)Read("nextWaveCountdown")==15,"Full preparation countdown restored");
                Call("BeginPreparationTutorial");Check(Step=="None","Lesson does not repeat after completion");
                game.ResetRun();Check(Step=="None","Restart clears tutorial");
                Set("wave",2);Call("BeginPreparationTutorial");Check(Step=="None","Later waves do not trigger lesson");
                Set("wave",1);Set("fighting",false);Call("BeginPreparationTutorial");Call("FinishPreparationTutorial");
                Check(Step=="None","Skip exits lesson");
                SessionState.SetInt(StageKey,0);EditorApplication.isPlaying=false;return;
            }
            SessionState.SetInt(StageKey,stage+1);next=EditorApplication.timeSinceStartup+3;
        }
        catch(Exception e){File.AppendAllText(Root+"PreparationChecks.txt","FAIL "+e);SessionState.SetInt(StageKey,0);Debug.LogException(e);}
    }
}
