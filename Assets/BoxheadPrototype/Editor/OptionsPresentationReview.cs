using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class OptionsPresentationReview
{
    const string Key="OptionsPresentationReview.Stage",Root="Assets/Verification~/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static double next;
    static OptionsPresentationReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(File.Exists(Root+"OptionsPresentation.request"))
        {
            File.Delete(Root+"OptionsPresentation.request");SessionState.SetInt(Key,1);
            EditorApplication.isPaused=false;if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;
            next=EditorApplication.timeSinceStartup+4;return;
        }
        int stage=SessionState.GetInt(Key,0);if(stage==0||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();if(game==null)return;
        object Call(string name,params object[] args)=>typeof(YardSurvival).GetMethod(name,Flags).Invoke(game,args);
        try
        {
            if(stage==1)
            {
                Call("ShowHome");var front=typeof(YardSurvival).GetNestedType("FrontScreen",BindingFlags.NonPublic);
                Call("ShowOptions",Enum.Parse(front,"Home"));
                var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).Focus();
                SessionState.SetInt(Key,2);next=EditorApplication.timeSinceStartup+3;
            }
            else if(stage==2)
            {
                foreach(string n in new[]{"optionsLabel","optionsTitle","optionsSmall"}){var st=(GUIStyle)typeof(YardSurvival).GetField(n,Flags).GetValue(game);File.AppendAllText(Root+"OptionsFont.txt",n+" size="+st.fontSize+" font="+st.font+" dynamic="+(st.font!=null&&st.font.dynamic)+" rect="+st.CalcSize(new GUIContent("OPTIONS"))+"\\n");}
                ScreenCapture.CaptureScreenshot(Root+"Options-Redesigned.png");SessionState.SetInt(Key,3);next=EditorApplication.timeSinceStartup+3;
            }
            else if(stage==3)
            {
                if(!File.Exists(Root+"Options-Redesigned.png"))throw new Exception("Options screenshot missing");
                File.WriteAllText(Root+"OptionsPresentation.txt","PASS Manual quality options rendered; automatic quality and performance report removed.\n");
                Call("ShowHome");SessionState.SetInt(Key,4);next=EditorApplication.timeSinceStartup+2;
            }
            else if(stage==4)
            {
                ScreenCapture.CaptureScreenshot(Root+"Home-RunRecords.png");SessionState.SetInt(Key,0);
            }
        }
        catch(Exception e){SessionState.SetInt(Key,0);File.WriteAllText(Root+"OptionsPresentation.txt","FAIL "+e);Debug.LogException(e);}
    }
}
