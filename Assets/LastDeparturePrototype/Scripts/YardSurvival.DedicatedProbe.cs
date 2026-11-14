#if DEVELOPMENT_BUILD && !UNITY_SERVER && !UNITY_WEBGL
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
namespace YardSurvivalPrototype
{
    // Audio reset stops coroutines on YardSurvival when entering the lobby.
    // Keep the test runner on its own component so the probe survives resets.
    public sealed class DedicatedProbeRunner : MonoBehaviour { }
    public sealed partial class YardSurvival
    {
        IEnumerator DedicatedNetworkProbe()
        {
            Debug.Log("PROBE starting");
            Application.runInBackground = true;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            yield return null;
            ConnectDedicatedServer(StartupArgument("-dustlineConnect"));
            Debug.Log("PROBE client started");
            float deadline = Time.realtimeSinceStartup + 40;
            while (!coopLobby && Time.realtimeSinceStartup < deadline) yield return null;
            if (!coopLobby) { FinishProbe("FAIL connection: " + coopStatus); yield break; }
            SetCoopReady(true);
            Debug.Log("PROBE ready sent");
            while (!coopStarted && Time.realtimeSinceStartup < deadline) yield return null;
            if (!coopStarted) { FinishProbe("FAIL match did not start"); yield break; }
            yield return new WaitForSecondsRealtime(1);
            Vector3 start = player.position;
            int sequence = receivedWorldSequence;
            CoopQAMove(StartupArgument("-dustlineDirection", "forward"));
            yield return new WaitForSecondsRealtime(2);
            CoopQAMove("stop");
            yield return new WaitForSecondsRealtime(1);
            float distance = Vector3.Distance(start,player.position);
            CoopQAPublish();
            bool ok = IsCoopGuest && receivedWorldSequence > sequence + 5 && distance > .5f && Time.unscaledTime-lastWorldAt < 2;
            FinishProbe((ok ? "PASS" : "FAIL") + " client=" + NetworkManagerLocalId() + " snapshots=" + receivedWorldSequence + " moved=" + distance + " partnerHealth=" + replicaPeerHealth);
        }
        void FinishProbe(string result)
        {
            Debug.Log("DEDICATED NETWORK PROBE " + result);
            string path = StartupArgument("-dustlineReport");
            if (path.Length > 0) File.WriteAllText(path,result);
            Application.Quit(result.StartsWith("PASS") ? 0 : 1);
        }
    }
}
#endif
