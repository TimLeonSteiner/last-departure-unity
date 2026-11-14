using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
[InitializeOnLoad]
public static class CoopPackageSetup
{
    static AddAndRemoveRequest request;
    static CoopPackageSetup(){EditorApplication.update+=Poll;}
    public static void Install()
    {
        if(EditorApplication.isPlaying)throw new System.Exception("Stop Play first");
        request=Client.AddAndRemove(new[]{"com.unity.netcode.gameobjects","com.unity.services.multiplayer"});
        File.WriteAllText("Assets/BoxheadPrototype/CoopPackages.txt","Installing Netcode for GameObjects and Multiplayer Services...");
    }
    static void Poll()
    {
        if(request==null||!request.IsCompleted)return;
        string message=request.Status==StatusCode.Success?"PASS: networking packages installed.":"FAIL: "+request.Error?.message;
        File.WriteAllText("Assets/BoxheadPrototype/CoopPackages.txt",message);request=null;
    }
}
