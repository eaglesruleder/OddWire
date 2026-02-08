using HarmonyLib;
using Vintagestory.API.Common;

namespace OddWire;
public class OddWireHarmony : ModSystem
{
    private Harmony? harmony;

    public override void Start(ICoreAPI api)
    {
        harmony = new Harmony("oddwire.harmony");
        harmony.PatchAll();
        base.Start(api);
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(harmony.Id);
        harmony = null;
        base.Dispose();
    }
}