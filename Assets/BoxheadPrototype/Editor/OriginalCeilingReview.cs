using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class OriginalCeilingReview
{
    static OriginalCeilingReview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        const string request="Assets/Verification~/OriginalCeiling.request";
        if(File.Exists(request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(request);SessionState.SetBool("OriginalCeiling.Review",true);
            EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool("OriginalCeiling.Review",false))return;
        EditorApplication.isPaused=false;
        Application.runInBackground=true;
        EditorApplication.QueuePlayerLoopUpdate();
        if(!EditorApplication.isPlaying||Time.unscaledTime<4)return;
        SessionState.SetBool("OriginalCeiling.Review",false);
        try
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            var hall=game.transform.Find("Spaceport terminal");
            if(hall.Find("Meshy roof")!=null)throw new Exception("Replacement roof is still active");
            if(FullRoofLayout.Load()!=null)throw new Exception("Replacement rain layout is still active");
            var racks=hall.Find("Textured server racks");
            if(racks==null||racks.childCount!=48)throw new Exception("Server racks were not preserved");
            string checks=game.RunWarehouseChecks();
            SpaceportAssetBuild.Capture(new Vector3(-13,3,-13),new Vector3(0,8,-1),"OriginalCeiling-Restored");
            File.WriteAllText("Assets/Verification~/OriginalCeilingReview.txt","PASS original terminal ceiling restored; 48 textured racks preserved; replacement roof and rain layout inactive.\n"+checks);
        }
        catch(Exception ex){File.WriteAllText("Assets/Verification~/OriginalCeilingReview.txt","FAIL "+ex);Debug.LogException(ex);}
        finally{EditorApplication.isPlaying=false;}
    }
}
