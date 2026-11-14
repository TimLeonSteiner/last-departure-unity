using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad] public static class RecoilReview
{
    static RecoilReview(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        const string pending="Dustline.RecoilReview";
        if(File.Exists("Assets/LastDeparturePrototype/RecoilReview.request")&&!EditorApplication.isPlaying)
        {
            File.Delete("Assets/LastDeparturePrototype/RecoilReview.request");
            SessionState.SetString(pending+".scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene("Assets/LastDeparture.unity");SessionState.SetInt(pending,1);EditorApplication.isPlaying=true;return;
        }
        if(SessionState.GetInt(pending,0)==1&&EditorApplication.isPlaying)
        {
            var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
            try{File.WriteAllText("Assets/LastDeparturePrototype/RecoilReview.txt",game.RunRecoilChecks());}
            catch(Exception e){File.WriteAllText("Assets/LastDeparturePrototype/RecoilReview.txt","FAIL "+e);}
            SessionState.SetInt(pending,2);EditorApplication.isPlaying=false;return;
        }
        if(SessionState.GetInt(pending,0)==2&&!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetInt(pending,0);string previous=SessionState.GetString(pending+".scene","");if(previous!="")EditorSceneManager.OpenScene(previous);
        }
    }
}
