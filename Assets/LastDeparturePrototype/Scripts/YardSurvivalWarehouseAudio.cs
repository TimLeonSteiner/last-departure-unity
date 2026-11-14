using UnityEngine;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        readonly AudioClip[] warehouseTails = new AudioClip[8];
        readonly AudioClip[] warehouseReflections = new AudioClip[8];
        readonly AudioSource[] warehouseVoices = new AudioSource[32];
        readonly float[] warehouseVoiceEnd = new float[32];
        readonly float[] warehouseVoiceGain = new float[32];
        static readonly Vector3[] ReflectionDirections = { Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.up };

        void SetUpWarehouseAudio()
        {
            for (int i = 0; i < warehouseTails.Length; i++)
            {
                warehouseTails[i] = Resources.Load<AudioClip>("WarehouseAudio/" + i + "_Tail");
                warehouseReflections[i] = Resources.Load<AudioClip>("WarehouseAudio/" + i + "_Reflection");
            }
            for (int i = 0; i < warehouseVoices.Length; i++)
            {
                var obj = new GameObject("Warehouse acoustic voice " + i); obj.transform.SetParent(transform, false);
                var source = obj.AddComponent<AudioSource>(); source.playOnAwake = false;
                source.dopplerLevel = 0; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1; source.maxDistance = 65; source.priority = 190;
                warehouseVoices[i] = source;
            }
        }
        void PlayWarehouseShot()
        {
            if (!soundEnabled || !audioUnlocked || arenaCamera == null || WeaponCatalog.IsMelee(weaponId) || weaponId == 7) return;
            int bank = ArsenalAudio[weaponId];
            Vector3 origin = arenaCamera.transform.position;
            float closest = float.PositiveInfinity, second = float.PositiveInfinity;
            Vector3 firstPoint = origin, secondPoint = origin;
            foreach (var direction in ReflectionDirections)
            {
                var ray = new Ray(origin, direction);
                float distanceToWall = float.PositiveInfinity;
                if(fullRoofCollider!=null && fullRoofCollider.Raycast(ray,out var roofHit,65))distanceToWall=roofHit.distance;
                foreach (var wall in wallVolumes)
                    if (wall.IntersectRay(ray, out float entry) && entry > .01f) distanceToWall = Mathf.Min(distanceToWall, entry);
                foreach (var obstacle in obstacles)
                {
                    var bounds = new Bounds(new Vector3(obstacle.center.x, 1.325f, obstacle.center.y), new Vector3(obstacle.width, 2.65f, obstacle.height));
                    if (bounds.IntersectRay(ray, out float entry) && entry > .01f) distanceToWall = Mathf.Min(distanceToWall, entry);
                }
                if (distanceToWall < closest)
                { second = closest; secondPoint = firstPoint; closest = distanceToWall; firstPoint = origin + direction * distanceToWall; }
                else if (distanceToWall < second) { second = distanceToWall; secondPoint = origin + direction * distanceToWall; }
            }
            if (closest < 40) QueueWarehouseVoice(warehouseReflections[bank], firstPoint, .16f / (1 + closest * .06f), Mathf.Max(.012f, closest * 2 / 343), true);
            if (second < 40) QueueWarehouseVoice(warehouseReflections[bank], secondPoint, .09f / (1 + second * .06f), Mathf.Max(.022f, second * 2 / 343), true);
            QueueWarehouseVoice(warehouseTails[bank], origin, .17f, .045f, false);
        }
        void QueueWarehouseVoice(AudioClip clip, Vector3 position, float gain, float delay, bool spatial)
        {
            if (clip == null) return;
            // A bounded pool permits overlapping tails without allocating per shot.
            int slot = -1;
            for (int i = 0; i < warehouseVoices.Length; i++)
                if (warehouseVoiceEnd[i] <= Time.unscaledTime) { slot = i; break; }
            if (slot < 0) return; // Never truncate an existing tail to add another reflection.
            var source = warehouseVoices[slot];
            source.transform.position = position; source.spatialBlend = spatial ? 1 : 0;
            source.clip = clip; source.pitch = 1;
            warehouseVoiceGain[slot] = gain; source.volume = gain * soundVolume;
            warehouseVoiceEnd[slot] = Time.unscaledTime + delay + clip.length;
            source.PlayDelayed(delay);
        }
        void ApplyWarehouseVolume()
        {
            for (int i = 0; i < warehouseVoices.Length; i++)
                if (warehouseVoices[i] != null) warehouseVoices[i].volume = warehouseVoiceGain[i] * soundVolume;
        }
        void StopWarehouseAudio()
        {
            for (int i = 0; i < warehouseVoices.Length; i++)
            {
                if (warehouseVoices[i] != null) warehouseVoices[i].Stop();
                warehouseVoiceEnd[i] = 0;
            }
        }
    }
}
