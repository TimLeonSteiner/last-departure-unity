using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class ReloadFeedbackReview
{
    static ReloadFeedbackReview(){EditorApplication.update+=Poll;}
    static void Poll(){const string root="Assets/Verification~/";if(!File.Exists(root+"ReloadFeedbackReview.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||!EditorApplication.isPlaying)return;File.Delete(root+"ReloadFeedbackReview.request");try{File.WriteAllText(root+"ReloadFeedbackReview.txt",UnityEngine.Object.FindAnyObjectByType<YardSurvival>().RunReloadFeedbackChecks());}catch(Exception e){File.WriteAllText(root+"ReloadFeedbackReview.txt","FAIL "+e);}}
}
