using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace OddWire
{
    public class OddWireHarmony : ModSystem
    {
        private Harmony? harmony;

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();

            base.Start(api);
        }

        public override void Dispose()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll(harmony.Id);
                harmony = null;
            }

            base.Dispose();
        }

        internal static void OnBarrelEvery3Seconds(BlockEntityBarrel barrel)
        {
        }
    }
}
