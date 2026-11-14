using UnityEngine;

namespace YardSurvivalPrototype
{
    [DisallowMultipleComponent]
    public sealed class ExplosiveBarrel : MonoBehaviour
    {
        [Min(.1f)] public float blastRadius = 4;
        [Min(1)] public int zombieDamage = 12;
        [Min(0)] public int playerDamage = 45;
        public Bounds localBounds = new Bounds(new Vector3(0, .45f, 0), new Vector3(.6f, .9f, .6f));
        public bool Detonated { get; private set; }
        [System.NonSerialized] public int placedId;
        public Vector3 Center => transform.TransformPoint(localBounds.center);
        public string SceneKey
        {
            get { if(placedId>0)return "placed:"+placedId; string key = ""; for (var t = transform; t != null; t = t.parent) key = "/" + t.GetSiblingIndex() + key; return gameObject.scene.path + key; }
        }
        void OnEnable()
        {
            if (!Application.isPlaying) return;
            var game = FindAnyObjectByType<YardSurvival>();
            if (game != null) game.RegisterExplosiveBarrel(this);
        }
        public void SetDetonated(bool value)
        {
            Detonated = value;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !value;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = !value;
        }
        public bool Trace(Vector3 origin, Vector3 direction, float limit, out float distance)
        {
            distance = 0;
            if (Detonated || !isActiveAndEnabled) return false;
            Vector3 o = transform.InverseTransformPoint(origin), d = transform.InverseTransformVector(direction);
            float low = 0, high = limit;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(d[axis]) < .000001f) { if (o[axis] < localBounds.min[axis] || o[axis] > localBounds.max[axis]) return false; continue; }
                float a = (localBounds.min[axis] - o[axis]) / d[axis], b = (localBounds.max[axis] - o[axis]) / d[axis];
                low = Mathf.Max(low, Mathf.Min(a, b)); high = Mathf.Min(high, Mathf.Max(a, b));
                if (low > high) return false;
            }
            distance = low; return low < limit;
        }
        public Rect Footprint
        {
            get
            {
                var b = new Bounds(Center, Vector3.zero);
                for (int i = 0; i < 8; i++) b.Encapsulate(transform.TransformPoint(localBounds.center + Vector3.Scale(localBounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                return new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
            }
        }
        void OnDrawGizmosSelected() { Gizmos.color = new Color(1, .35f, .05f, .7f); Gizmos.DrawWireSphere(Center, blastRadius); }
    }
}
