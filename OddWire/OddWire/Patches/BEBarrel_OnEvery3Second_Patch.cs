using HarmonyLib;
using Vintagestory.GameContent;

namespace OddWire.Patches
{
    [HarmonyPatch(typeof(BlockEntityBarrel), "OnEvery3Second")]
    public static class BEBarrel_OnEvery3Second_Patch
    {
        public static void Postfix(BlockEntityBarrel __instance)
        {
            OddWireHarmony.OnBarrelEvery3Seconds(__instance);
        }
    }
}
