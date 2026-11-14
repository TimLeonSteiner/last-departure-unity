using System;using System.IO;using System.Reflection;using UnityEditor;using UnityEngine;using YardSurvivalPrototype;
[InitializeOnLoad]public static class WeatherOpticsReview{
static WeatherOpticsReview(){EditorApplication.update+=Poll;}
static void Poll(){const string request="Assets/BoxheadPrototype/WeatherOpticsReview.request";if(!File.Exists(request)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete(request);var game=UnityEngine.Object.FindAnyObjectByType<YardSurvival>();bool old=game.enabled;try{game.enabled=false;typeof(YardSurvival).GetMethod("PlayFromMenu",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);File.WriteAllText("Assets/BoxheadPrototype/WeatherOpticsReview.txt",game.RunWeatherOpticsReview());}catch(Exception e){File.WriteAllText("Assets/BoxheadPrototype/WeatherOpticsReview.txt","FAIL "+e);Debug.LogException(e);}finally{game.enabled=old;}}
}
