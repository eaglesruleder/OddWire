using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#nullable disable

namespace OddWire.GameContent
{
    // BEBrazier now uses FirepitContentsRenderer directly.
    // This class is kept as a named alias for any external references that depend on the type name.
    // FirepitContentsRenderer is functionally identical and stays in sync with vanilla.
    public class BrazierContentsRenderer : FirepitContentsRenderer
    {
        public BrazierContentsRenderer(ICoreClientAPI api, BlockPos pos) : base(api, pos) { }
    }
}
