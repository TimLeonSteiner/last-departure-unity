using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed class EntranceBoards : MonoBehaviour
    {
        readonly GameObject[] boards = new GameObject[5];
        int visible = 5;
        float lastHealth = 100;
        YardSurvival owner;
        public const float DoorDepth = 18 * YardSurvival.ArenaScale - .9f;
        public static bool IsDoorway(Vector3 p) => Mathf.Abs(Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) - DoorDepth) < .1f && Mathf.Abs(Mathf.Min(Mathf.Abs(p.x), Mathf.Abs(p.z)) - 1.4f) < .1f;
        public static Transform Create(YardSurvival game, Transform parent, Vector3 position, bool vertical)
        {
            if (IsDoorway(position))
            {
                float side = vertical ? -Mathf.Sign(position.z) : Mathf.Sign(position.x);
                var fullHeight = Resources.Load<GameObject>("EntranceBarricade/" + (side < 0 ? "Left" : "Right"));
                if (fullHeight != null)
                {
                    var instance = Instantiate(fullHeight, parent);
                    instance.transform.localPosition = position;
                    instance.transform.localRotation = Quaternion.Euler(0, vertical ? 90 : 0, 0);
                    TrackImported(instance, game);
                    return instance.transform;
                }
            }
            var root = new GameObject("Breakable entrance barricade").transform;
            root.SetParent(parent, false); root.localPosition = position;
            root.localRotation = Quaternion.Euler(0, vertical ? 90 : 0, 0);
            var state = root.gameObject.AddComponent<EntranceBoards>();
            state.owner = game;
            if (IsDoorway(position))
            {
                float outer = vertical ? -Mathf.Sign(position.z) : Mathf.Sign(position.x);
                var cleat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cleat.name = "Timber fixing strip against doorway jamb";
                cleat.transform.SetParent(root, false);
                cleat.transform.localPosition = new Vector3(outer * 2.1f, .55f, .10f);
                cleat.transform.localScale = new Vector3(.30f, 2.3f, .40f);
                cleat.GetComponent<Collider>().enabled = false;
                cleat.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("EntrancePlanks/AgedWood") ?? game.woodMaterial;
            }
            for (int i = 0; i < 5; i++)
            {
                var detailed = Resources.Load<GameObject>("EntrancePlanks/Board" + i);
                if (detailed != null)
                {
                    var board = Instantiate(detailed, root);
                    board.name = "Blender salvaged entrance board " + (i + 1);
                    board.transform.localPosition = new Vector3(0, -.20f + i * .38f, (vertical ? -position.z : position.x) < 0 ? -.13f : 0);
                    board.transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 2 : -2);
                    state.boards[i] = board;
                    continue;
                }
                var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = "Salvaged entrance plank " + (i + 1);
                plank.transform.SetParent(root, false);
                plank.transform.localPosition = new Vector3(0, -.20f + i * .38f, (vertical ? -position.z : position.x) < 0 ? -.13f : 0);
                plank.transform.localScale = new Vector3(4.5f, .25f, .12f);
                plank.transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 2 : -2);
                plank.GetComponent<Collider>().enabled = false;
                plank.GetComponent<Renderer>().sharedMaterial = game.woodMaterial;
                state.boards[i] = plank;
                // Nails follow each board when it breaks away. Both faces are fastened.
                foreach (float end in new[] { -.455f, .455f })
                    foreach (float y in new[] { -.25f, .25f })
                        foreach (float face in new[] { -.54f, .54f })
                        {
                            var nail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            nail.name = "Recessed iron nail head";
                            nail.transform.SetParent(plank.transform, false);
                            nail.transform.localPosition = new Vector3(end, y, face);
                            nail.transform.localScale = new Vector3(.009f, .16f, .12f);
                            nail.GetComponent<Collider>().enabled = false;
                            nail.GetComponent<Renderer>().sharedMaterial = game.wallMaterial;
                        }
            }
            return root;
        }
        public static void TrackImported(GameObject instance, YardSurvival game)
        {
            var state = instance.AddComponent<EntranceBoards>(); state.owner = game;
            for (int i=0;i<5;i++) state.boards[i] = instance.transform.Find("Damage " + i)?.gameObject;
        }
        public void SetHealth(float hp)
        {
            int count = Mathf.Clamp(Mathf.CeilToInt(hp / 20), 0, 5);
            if (hp < lastHealth && owner != null) owner.PlayBarricadeDamage(transform.position, count < visible, hp <= 0);
            lastHealth = hp;
            if (visible == count) return;
            for (int i = 0; i < boards.Length; i++)
            {
                if (boards[i] == null) continue;
                if (i >= count && i < visible && Application.isPlaying)
                {
                    var debris = Instantiate(boards[i], transform.position, transform.rotation);
                    debris.transform.position = boards[i].transform.position;
                    debris.transform.rotation = boards[i].transform.rotation;
                    debris.transform.localScale = boards[i].transform.lossyScale;
                    // Cosmetic fragments cannot obstruct the doorway or the AI.
                    var body = debris.AddComponent<Rigidbody>();
                    body.useGravity = true; body.linearVelocity = transform.forward * 1.5f + Vector3.up;
                    body.angularVelocity = new Vector3(2, 1, 3);
                    Destroy(debris, 1.2f);
                }
                boards[i].SetActive(i < count);
            }
            visible = count;
        }
    }
}

