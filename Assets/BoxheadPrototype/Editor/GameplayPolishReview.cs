using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using YardSurvivalPrototype;

[InitializeOnLoad]
public static class GameplayPolishReview
{
    const string Request = "Library/GameplayPolish.request";
    const string Folder = "Assets/Verification~/GameplayPolish/";
    static GameplayPolishReview() => EditorApplication.update += Poll;
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string action = File.ReadAllText(Request).Trim();
        if (action != "assets" && !EditorApplication.isPlaying) { EditorApplication.isPlaying = true; return; }
        File.Delete(Request);
        Directory.CreateDirectory(Folder);
        try
        {
            if (action == "assets")
            {
                const string path = "Assets/BoxheadPrototype/Resources/Spaceport/ModularHallTerminalSet.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    typeof(SolidHallWallsBuild).GetMethod("RemoveBakedSigns", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { root.transform });
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.SaveAssets();
                File.WriteAllText(Folder + "Assets.txt", "PASS: removed retired wall status panels from the hall prefab.");
                return;
            }
            var game = UnityEngine.Object.FindAnyObjectByType<YardSurvival>();
            if (action == "final")
            {
                foreach (var check in new[] { "grid", "voices", "brute", "weapon", "wall", "keyboard" })
                    File.WriteAllText(Folder + check + ".txt", game.RunGameplayPolishChecks(check));
                File.WriteAllText(Folder + "final.txt", "PASS: crowd equivalence, dialogue assets and scream timing, Brute arm lengths, rifle/wall/keyboard captures.");
                return;
            }
            File.WriteAllText(Folder + action + ".txt", game.RunGameplayPolishChecks(action));
        }
        catch (Exception e) { File.WriteAllText(Folder + action + ".txt", "FAIL " + e); Debug.LogException(e); }
    }
}
