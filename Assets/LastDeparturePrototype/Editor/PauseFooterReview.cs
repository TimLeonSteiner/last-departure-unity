using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class PauseFooterReview
{
    const string Root="Assets/Verification~/PauseFooter/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static EditorWindow view;
    static object group, oldScreen;
    static YardSurvival game;
    static int oldSize, stage, customIndex;
    static bool oldCredits;
    static double next;
    static readonly Vector2Int[] Sizes={new Vector2Int(640,360),new Vector2Int(1280,720),new Vector2Int(417,225)};
    static PauseFooterReview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            if(stage==0)
            {
                if(!File.Exists(Root+"Capture.request"))return;
                File.Delete(Root+"Capture.request");
                if(!EditorApplication.isPlaying)throw new Exception("Active play session required; review does not restart runs.");
                game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
                oldScreen=typeof(YardSurvival).GetField("frontScreen",Flags).GetValue(game);
                oldCredits=(bool)typeof(YardSurvival).GetField("audioCreditsVisible",Flags).GetValue(game);
                typeof(YardSurvival).GetField("frontScreen",Flags).SetValue(game,Enum.Parse(oldScreen.GetType(),"Pause"));
                typeof(YardSurvival).GetField("audioCreditsVisible",Flags).SetValue(game,false);
                var asm=typeof(EditorWindow).Assembly;
                view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
                oldSize=(int)view.GetType().GetProperty("selectedSizeIndex",Flags).GetValue(view);
                var sizesType=asm.GetType("UnityEditor.GameViewSizes");
                var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
                var groupType=sizesType.GetProperty("currentGroupType",Flags).GetValue(sizes);
                group=sizesType.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{groupType});
                // Remove only temporary sizes from earlier interrupted reviews.
                int total=(int)group.GetType().GetMethod("GetTotalCount",Flags).Invoke(group,null);
                int builtin=(int)group.GetType().GetMethod("GetBuiltinCount",Flags).Invoke(group,null);
                for(int i=total-1;i>=builtin;i--)
                {
                    var existing=group.GetType().GetMethod("GetGameViewSize",Flags).Invoke(group,new object[]{i});
                    string name=(string)existing.GetType().GetProperty("baseText",Flags).GetValue(existing);
                    if(name=="Pause footer review")group.GetType().GetMethod("RemoveCustomSize",Flags).Invoke(group,new object[]{i});
                }
                File.WriteAllText(Root+"Results.txt","");
                stage=1;SetSize(0);return;
            }
            if(EditorApplication.timeSinceStartup<next)return;
            int index=(stage-1)/2;
            if(stage%2==1)
            {
                ScreenCapture.CaptureScreenshot(Root+Sizes[index].x+"x"+Sizes[index].y+".png");
                stage++;next=EditorApplication.timeSinceStartup+2;
            }
            else
            {
                var capture=new Texture2D(2,2);
                ImageConversion.LoadImage(capture,File.ReadAllBytes(Root+Sizes[index].x+"x"+Sizes[index].y+".png"));
                if(capture.width!=Sizes[index].x||capture.height!=Sizes[index].y)throw new Exception("Capture size mismatch");
                File.AppendAllText(Root+"Results.txt","PASS rendered "+capture.width+"x"+capture.height+"\n");
                UnityEngine.Object.DestroyImmediate(capture);
                RemoveSize();
                if(index+1<Sizes.Length){stage++;SetSize(index+1);}
                else {Restore();File.AppendAllText(Root+"Results.txt","PASS captures complete; original game-view size and menu restored.\n");}
            }
        }
        catch(Exception e){File.WriteAllText(Root+"Error.txt",e.ToString());Restore();}
    }
    static void SetSize(int i)
    {
        var asm=typeof(EditorWindow).Assembly;
        var type=asm.GetType("UnityEditor.GameViewSize");
        var kind=Enum.ToObject(asm.GetType("UnityEditor.GameViewSizeType"),1);
        var size=Activator.CreateInstance(type,Flags,null,new object[]{kind,Sizes[i].x,Sizes[i].y,"Pause footer review"},null);
        customIndex=(int)group.GetType().GetMethod("GetTotalCount",Flags).Invoke(group,null);
        group.GetType().GetMethod("AddCustomSize",Flags).Invoke(group,new[]{size});
        view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,customIndex);
        view.Focus();view.Repaint();next=EditorApplication.timeSinceStartup+3;
    }
    static void RemoveSize()
    {
        view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,oldSize);
        group.GetType().GetMethod("RemoveCustomSize",Flags).Invoke(group,new object[]{customIndex});
    }
    static void Restore()
    {
        stage=0;
        if(game!=null&&oldScreen!=null){typeof(YardSurvival).GetField("frontScreen",Flags).SetValue(game,oldScreen);typeof(YardSurvival).GetField("audioCreditsVisible",Flags).SetValue(game,oldCredits);}
        if(view!=null)view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,oldSize);
    }
}
