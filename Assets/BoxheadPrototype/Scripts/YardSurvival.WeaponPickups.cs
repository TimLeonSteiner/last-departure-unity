using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        Material waveWeaponHologram;

        Pickup SpawnPickup(int id, Vector3 position, int rounds, int reserve = -1, bool waveDrop = false)
        {
            if (!WeaponCatalog.IsAvailable(id)) return null;
            var root = new GameObject("Wave weapon: " + WeaponCatalog.All[id].name).transform;
            root.SetParent(actors, false); root.position = position + Vector3.up * .7f;
            if (!dedicatedServer && ModelFor(id) != null)
            {
                var model = Instantiate(ModelFor(id), root).transform;
                model.GetComponent<AnimatedWeaponRig>()?.GroundPose();
                foreach (var a in model.GetComponentsInChildren<Animator>()) a.enabled = false;
                foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
                foreach (var body in model.GetComponentsInChildren<Rigidbody>()) { body.isKinematic = true; body.detectCollisions = false; }
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                var bounds = new Bounds(); bool first = true;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
                if (!first)
                {
                    float scale = 1.1f / Mathf.Max(.1f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                    Vector3 offset = root.InverseTransformVector(bounds.center - model.position) * scale;
                    model.localScale *= scale; model.localPosition = -offset;
                }
                if (waveDrop)
                {
                    if (waveWeaponHologram == null)
                        waveWeaponHologram = new Material(Resources.Load<Shader>("Inventory/WeaponPickupHologram"));
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    {
                        if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++) materials[i] = waveWeaponHologram;
                        renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
                var lamp = root.gameObject.AddComponent<Light>();
                lamp.color = new Color(.25f,.7f,1); lamp.range = 2.5f; lamp.intensity = 1.2f; lamp.shadows = LightShadows.None;
            }
            var pickup = new Pickup { body = root, position = position, weapon = id,
                ammo = Mathf.Clamp(rounds,0,WeaponCatalog.All[id].capacity),
                reserve = reserve < 0 ? WeaponCatalog.All[id].capacity * 3 : reserve, netId = nextCoopEntity++, waveDrop = waveDrop };
            pickups.Add(pickup); return pickup;
        }
        bool InWeaponPickupRange(Vector3 position, Pickup pickup) => pickup != null &&
            (new Vector3(position.x,0,position.z) - new Vector3(pickup.position.x,0,pickup.position.z)).sqrMagnitude <= 2.3f * 2.3f &&
            PickupLineClear(position,pickup.position);
        Pickup NearbyPickup()
        {
            if (player == null) return null;
            Pickup nearest = null; float distance = float.MaxValue;
            foreach (var pickup in pickups)
                if (InWeaponPickupRange(player.position,pickup))
                {
                    float d = (player.position - pickup.position).sqrMagnitude;
                    if (d < distance) { nearest = pickup; distance = d; }
                }
            return nearest;
        }
        void RemoveWeaponPickup(Pickup pickup)
        {
            pickups.Remove(pickup);
            if (pickup == weaponTrailTarget) HideWeaponTrail();
            if (pickup.body != null) { pickup.body.gameObject.SetActive(false); Destroy(pickup.body.gameObject); }
        }
        bool TakePickup(Pickup pickup)
        {
            if (!InventoryInputAllowed || upgradePending || building || reloading || pickup == null ||
                !pickups.Contains(pickup) || !InWeaponPickupRange(player.position,pickup)) return false;
            if (IsCoopGuest) { SendCoopCommand("pickup",pickup.netId); return true; }
            EquipWeapon(pickup.weapon,pickup.ammo,pickup.reserve);
            fireTimer=Mathf.Max(.18f,pickup.readyAt-Time.time);
            RemoveWeaponPickup(pickup);
            hologramRenderedTab=-1;
            ShowBanner("PICKED UP: " + ActiveWeapon.name); PlayEffect(WeaponClip(2),.4f);
            return true;
        }
        void UpdateWeaponPickups()
        {
            UpdateWeaponTrail();
            foreach (var pickup in pickups)
                if (pickup.body != null)
                {
                    pickup.body.position = pickup.position + Vector3.up * (.7f + Mathf.Sin(Time.time * 2) * .07f);
                    pickup.body.rotation = Quaternion.Euler(0,Time.time * 25,0);
                }
        }
    }
}
