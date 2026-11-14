using UnityEngine;

namespace YardSurvivalPrototype
{
    // Rain patches are baked against the imported mesh, in legacy arena coordinates.
    public sealed class FullRoofLayout : ScriptableObject
    {
        public Rect[] rainPatches;
        public static FullRoofLayout Load() => Resources.Load<FullRoofLayout>("Spaceport/ModularHall/Layout") ?? Resources.Load<FullRoofLayout>("Spaceport/FullRoof/Layout");
    }
}
