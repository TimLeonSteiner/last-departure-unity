using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public sealed class LastDepartureAppIcon : IPreprocessBuildWithReport
{
    const string IconPath = "Assets/LastDeparturePrototype/Branding/LastDepartureIcon.png";
    public int callbackOrder => 0;
    static LastDepartureAppIcon() { EditorApplication.delayCall += Apply; }
    public void OnPreprocessBuild(BuildReport report) => Apply();

    [MenuItem("Tools/Yard Survival/Apply application icon")]
    public static void Apply()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) throw new BuildFailedException("Application icon missing: " + IconPath);
        foreach (var group in new[] { NamedBuildTarget.Unknown, NamedBuildTarget.Standalone })
        {
            var sizes = PlayerSettings.GetIconSizes(group, IconKind.Any);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIcons(group, icons, IconKind.Any);
        }
        AssetDatabase.SaveAssets();
    }
}
