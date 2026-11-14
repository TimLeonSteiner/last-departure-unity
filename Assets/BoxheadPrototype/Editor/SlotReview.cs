using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad] public static class SlotReview
{
    static SlotReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists("Assets/BoxheadPrototype/SlotPreview.request")&&EditorApplication.isPlaying)
        {
            var preview=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(preview==null)return;
            File.Delete("Assets/BoxheadPrototype/SlotPreview.request");preview.enabled=true;preview.PrepareSlotPreview();return;
        }
        const string pending="Dustline.SlotReview";
        if(File.Exists("Assets/BoxheadPrototype/SlotReview.request")&&!EditorApplication.isPlaying)
        {
            File.Delete("Assets/BoxheadPrototype/SlotReview.request");
            SessionState.SetString(pending+".scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene("Assets/Boxhead.unity");SessionState.SetInt(pending,1);EditorApplication.isPlaying=true;return;
        }
        if(SessionState.GetInt(pending,0)==1&&EditorApplication.isPlaying)
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
            try{File.WriteAllText("Assets/BoxheadPrototype/SlotReview.txt",game.RunSlotChecks());}
            catch(Exception e){File.WriteAllText("Assets/BoxheadPrototype/SlotReview.txt","FAIL "+e);}
            SessionState.SetInt(pending,2);EditorApplication.isPlaying=false;return;
        }
        if(SessionState.GetInt(pending,0)==2&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetInt(pending,0);string previous=SessionState.GetString(pending+".scene","");if(previous!="")EditorSceneManager.OpenScene(previous);
        }
    }
}
