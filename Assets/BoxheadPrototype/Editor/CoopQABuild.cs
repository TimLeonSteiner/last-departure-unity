using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
[InitializeOnLoad]
public static class CoopQABuild
{
    static double next;
    static CoopQABuild(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        next=EditorApplication.timeSinceStartup+1;
        const string request="Assets/BoxheadPrototype/CoopQA.request";
        if(!File.Exists(request))return;
        string command=File.ReadAllText(request).Trim();File.Delete(request);
        if(command!="build")return;
        try{Build();}catch(Exception e){File.WriteAllText("Assets/BoxheadPrototype/CoopQABuild.txt","FAIL "+e);Debug.LogException(e);}
    }
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before QA build");
        string output=Path.Combine(Path.GetTempPath(),"Dustline-CoopQA");
        var compression=PlayerSettings.WebGL.compressionFormat;
        BuildReport report;
        try
        {
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
            report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Boxhead.unity"},locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.Development});
        }
        finally{PlayerSettings.WebGL.compressionFormat=compression;}
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("QA WebGL build "+report.summary.result+" errors="+report.summary.totalErrors);
        string index=Path.Combine(output,"index.html"),html=File.ReadAllText(index);
        var pattern=new Regex(@"\.then\s*\(\s*\(unityInstance\)\s*=>\s*\{");
        if(!pattern.IsMatch(html))throw new Exception("QA template unityInstance callback not found");
        html=pattern.Replace(html,m=>m.Value+"window.dustlineUnity=unityInstance;",1);
        string controls=File.ReadAllText("Assets/Verification~/CoopQA/qa-controls.html");
        html=html.Replace("</body>",controls+"</body>");File.WriteAllText(index,html);
        File.Copy("Assets/BoxheadPrototype/ThirdParty/AnimatedArsenal/CREDITS.txt",Path.Combine(output,"weapon-credits.txt"),true);
        File.WriteAllText("Assets/BoxheadPrototype/CoopQABuild.txt","PASS "+DateTime.Now.ToString("s")+" development-only local WebGL QA with visible host/join/state controls; output="+output+"\n");
    }
}
