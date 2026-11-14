using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class MeshyDreamRosterSetup
{
    const string Request="Assets/Verification~/PrepareMeshyDreamRoster.request";
    static MeshyDreamRosterSetup() { EditorApplication.update+=Tick; }
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying)return;
        File.Delete(Request); Run();
    }
    public static void Run()
    {
        var names=new[]{"Spitter","Screamer","ArmoredWorker","Dockworker","Limper","Twitcher","Brute","Crawler","Skitter","Gnawer"};
        var heights=new[]{1.9f,2.15f,1.9f,1.85f,1.8f,1.9f,2.15f,.8f,.95f,.85f};
        Directory.CreateDirectory("Assets/Verification~/MeshyDreamRoster");
        var report=new System.Text.StringBuilder();
        for(int i=0;i<names.Length;i++)
        {
            string name=names[i];
            if(!File.Exists("Assets/LastDeparturePrototype/ThirdParty/MeshyDream/"+name+"/"+name+".fbx"))
            { report.AppendLine("MISSING EXPORT "+name); continue; }
            try
            {
                MeshyDreamChargerSetup.BuildModel(name,heights[i],i<3);
                bool passed=NightmareCloudReview.RunFor(name);
                report.AppendLine((passed ? "PASS " : "FAIL ")+name);
            }
            catch(Exception e) { report.AppendLine("FAIL "+name+": "+e);Debug.LogException(e); }
            File.WriteAllText("Assets/Verification~/MeshyDreamRoster/Status.txt",report.ToString());
        }
        File.WriteAllText("Assets/Verification~/MeshyDreamRoster/Status.txt",report.ToString());
    }
}
