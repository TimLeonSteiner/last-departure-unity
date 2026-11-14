using UnityEngine;

namespace YardSurvivalPrototype
{
    static class FullscreenStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Start fullscreen even when Unity remembers an older windowed run.
            // Alt+Enter remains available for switching during this session.
            var resolution = Screen.currentResolution;
            Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.FullScreenWindow);
#endif
        }
    }
}
