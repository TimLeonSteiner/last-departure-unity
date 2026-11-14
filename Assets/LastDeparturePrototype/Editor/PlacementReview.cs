using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;
[InitializeOnLoad]
public static class PlacementReview
{
    static PlacementReview(){EditorApplication.update+=Poll;}
    static void Poll(){const string root="Assets/Verification~/";if(!File.Exists(root+"PlacementReview.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||!EditorApplication.isPlaying)return;File.Delete(root+"PlacementReview.request");try{File.WriteAllText(root+"PlacementReview.txt",UnityEngine.Object.FindAnyObjectByType<YardSurvival>().RunPlacementChecks());}catch(Exception e){File.WriteAllText(root+"PlacementReview.txt","FAIL "+e);}}
}
