using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YardSurvivalPrototype
{
    public sealed partial class YardSurvival
    {
        Mesh weaponTrailMesh;
        Material weaponTrailMaterial;
        MeshRenderer weaponTrailRenderer;
        float nextWeaponTrailUpdate;
        Pickup weaponTrailTarget;
        Vector3 weaponTrailStart;
        void DisposeWeaponTrail()
        {
            if (weaponTrailMesh != null) Destroy(weaponTrailMesh);
            if (weaponTrailMaterial != null) Destroy(weaponTrailMaterial);
        }

        void HideWeaponTrail()
        {
            if (weaponTrailRenderer != null) weaponTrailRenderer.enabled = false;
            weaponTrailTarget = null;
        }

        void UpdateWeaponTrail()
        {
            if (dedicatedServer || player == null) return;
            Pickup target = pickups.Find(p => p.waveDrop && p.body != null);
            if (target == null) { HideWeaponTrail(); return; }
            Vector3 start = player.position;
            start.y = 0;
            if (target == weaponTrailTarget && Time.unscaledTime < nextWeaponTrailUpdate) return;
            bool moved = target != weaponTrailTarget || (start - weaponTrailStart).sqrMagnitude >= .25f;
            // Refresh occasionally even when stationary so changing defenses can reroute the trail.
            if (!moved && Time.unscaledTime < nextWeaponTrailUpdate + 1.5f) return;
            weaponTrailTarget = target;
            weaponTrailStart = start;
            nextWeaponTrailUpdate = Time.unscaledTime + .5f;
            var route = FindWeaponTrail(start, target.position);
            if (route.Count < 2)
            {
                if (weaponTrailRenderer != null) weaponTrailRenderer.enabled = false;
                return;
            }
            route = RoundWeaponTrailCorners(route);
            // Sample the floor along long segments so the ribbon follows ramps.
            for (int i = route.Count - 1; i > 0; i--)
            {
                Vector3 startPoint = route[i - 1], endPoint = route[i];
                int steps = Mathf.CeilToInt(Vector3.Distance(startPoint, endPoint) / .5f);
                for (int step = steps - 1; step > 0; step--)
                    route.Insert(i, Vector3.Lerp(startPoint, endPoint, step / (float)steps));
            }
            if (route.Count < 2)
            {
                if (weaponTrailRenderer != null) weaponTrailRenderer.enabled = false;
                return;
            }

            if (weaponTrailRenderer == null)
            {
                var trail = new GameObject("Wave weapon floor trail");
                trail.transform.SetParent(transform, false);
                weaponTrailMesh = new Mesh { name = "Wave weapon floor line" };
                weaponTrailMesh.MarkDynamic();
                trail.AddComponent<MeshFilter>().sharedMesh = weaponTrailMesh;
                weaponTrailRenderer = trail.AddComponent<MeshRenderer>();
                weaponTrailMaterial = new Material(Resources.Load<Shader>("WeaponTrail"));
                weaponTrailMaterial.SetColor("_BaseColor", new Color(.22f, .85f, 1f));
                weaponTrailRenderer.sharedMaterial = weaponTrailMaterial;
                weaponTrailRenderer.shadowCastingMode = ShadowCastingMode.Off;
                weaponTrailRenderer.receiveShadows = false;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var lineCoordinates = new List<Vector3>();
            float remaining = 0f;
            for (int i = 1; i < route.Count; i++)
                remaining += Vector3.Distance(route[i - 1], route[i]);

            const float halfWidth = .15f;
            float distance = 0f;
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 point = route[i];
                Vector3 incoming = i == 0 ? route[1] - point : point - route[i - 1];
                Vector3 outgoing = i == route.Count - 1 ? incoming : route[i + 1] - point;
                Vector3 inSide = Vector3.Cross(Vector3.up, incoming).normalized;
                Vector3 outSide = Vector3.Cross(Vector3.up, outgoing).normalized;
                Vector3 bisector = (inSide + outSide).normalized;
                Vector3 offset = bisector * (halfWidth / Mathf.Max(.5f, Vector3.Dot(bisector, inSide)));
                if (i > 0)
                {
                    distance += incoming.magnitude;
                    int first = vertices.Count - 2;
                    triangles.Add(first);
                    triangles.Add(first + 2);
                    triangles.Add(first + 1);
                    triangles.Add(first + 1);
                    triangles.Add(first + 2);
                    triangles.Add(first + 3);
                }
                Vector3 proxy = point;
                proxy.y = GroundHeight(point) + .13f;
                vertices.Add(transform.InverseTransformPoint(proxy - offset));
                vertices.Add(transform.InverseTransformPoint(proxy + offset));
                lineCoordinates.Add(new Vector3(-1f, distance, distance - remaining));
                lineCoordinates.Add(new Vector3(1f, distance, distance - remaining));
            }
            weaponTrailMesh.Clear();
            weaponTrailMesh.SetVertices(vertices);
            weaponTrailMesh.SetUVs(0, lineCoordinates);
            weaponTrailMesh.SetTriangles(triangles, 0);
            weaponTrailRenderer.enabled = triangles.Count > 0;
        }

        List<Vector3> RoundWeaponTrailCorners(List<Vector3> route)
        {
            var rounded = new List<Vector3> { route[0] };
            for (int i = 1; i < route.Count - 1; i++)
            {
                Vector3 corner = route[i];
                Vector3 incoming = route[i - 1] - corner;
                Vector3 outgoing = route[i + 1] - corner;
                float trim = Mathf.Min(.65f, incoming.magnitude * .33f, outgoing.magnitude * .33f);
                Vector3 entry = corner + incoming.normalized * trim;
                Vector3 exit = corner + outgoing.normalized * trim;
                if (trim < .02f || !WeaponTrailSegmentClear(entry, exit))
                {
                    if ((corner - rounded[rounded.Count - 1]).sqrMagnitude > .0001f) rounded.Add(corner);
                    continue;
                }
                for (int sample = 0; sample <= 8; sample++)
                {
                    float t = sample / 8f;
                    rounded.Add(Vector3.Lerp(Vector3.Lerp(entry, corner, t), Vector3.Lerp(corner, exit, t), t));
                }
            }
            if ((route[route.Count - 1] - rounded[rounded.Count - 1]).sqrMagnitude > .0001f)
                rounded.Add(route[route.Count - 1]);
            return rounded;
        }

        bool WeaponTrailSegmentClear(Vector3 from, Vector3 to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .24f));
            for (int i = 0; i <= steps; i++)
                if (!Free(Vector3.Lerp(from, to, i / (float)steps), PlayerRadius, true)) return false;
            return true;
        }

        List<Vector3> FindWeaponTrail(Vector3 start, Vector3 target)
        {
            var route = new List<Vector3>();
            if (WeaponTrailSegmentClear(start, target))
            {
                route.Add(start);
                route.Add(target);
                return route;
            }

            Vector2Int source = Cell(start), goal = Cell(target);
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                Vector2Int cell = source + new Vector2Int(x, z);
                if (!IsWeaponTrailCell(cell) || !WeaponTrailSegmentClear(start, World(cell))) continue;
                previous.Add(cell, cell);
                queue.Enqueue(cell);
            }

            while (queue.Count > 0 && !previous.ContainsKey(goal))
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in Neighbours)
                {
                    Vector2Int next = current + direction;
                    if (!IsWeaponTrailCell(next) || previous.ContainsKey(next) ||
                        !WeaponTrailSegmentClear(World(current), World(next))) continue;
                    previous.Add(next, current);
                    queue.Enqueue(next);
                }
            }

            if (!previous.ContainsKey(goal) || !WeaponTrailSegmentClear(World(goal), target)) return route;
            route.Add(target);
            for (Vector2Int cell = goal; ; cell = previous[cell])
            {
                route.Add(World(cell));
                if (previous[cell] == cell) break;
            }
            route.Add(start);
            route.Reverse();
            for (int i = 0; i + 2 < route.Count;)
            {
                int shortcut = route.Count - 1;
                while (shortcut > i + 1 && !WeaponTrailSegmentClear(route[i], route[shortcut])) shortcut--;
                if (shortcut > i + 1) route.RemoveRange(i + 1, shortcut - i - 1);
                i++;
            }
            return route;
        }

        static bool IsWeaponTrailCell(Vector2Int cell) =>
            cell.x >= 0 && cell.x < GridSize && cell.y >= 0 && cell.y < GridSize;
    }
}
