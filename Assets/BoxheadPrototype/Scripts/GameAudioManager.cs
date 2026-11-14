using UnityEngine;

namespace YardSurvivalPrototype
{
    // Scene-scoped singleton: the current game owns its lifetime, so restarting
    // the scene cannot leave an old soundtrack playing behind the new one.
    [DisallowMultipleComponent]
    public sealed class GameAudioManager : MonoBehaviour
    {
        public static GameAudioManager Instance { get; private set; }
        public AudioSource Music { get; private set; }
        public AudioSource CrossfadeMusic { get; private set; }
        public AudioSource Effects { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstance() => Instance = null;

        public static GameAudioManager GetOrCreate(Transform owner)
        {
            if (Instance != null) return Instance;
            var root = new GameObject("Game audio manager");
            root.transform.SetParent(owner, false);
            return root.AddComponent<GameAudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Reject the component before it can create or play any sources.
                enabled = false;
                Destroy(this);
                return;
            }
            Instance = this;
            Music = CreateSource("Imported adaptive soundtrack", true);
            CrossfadeMusic = CreateSource("Adaptive soundtrack crossfade", true);
            Effects = CreateSource("Game sound effects", false);
        }

        AudioSource CreateSource(string label, bool loop)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.loop = loop;
            return source;
        }

        public void PlayEffect(AudioClip clip, float gain)
        {
            if (Instance == this && clip != null) Effects.PlayOneShot(clip, gain);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
