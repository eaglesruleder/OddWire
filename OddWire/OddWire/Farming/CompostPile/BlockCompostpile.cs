using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace OddWire.GameContent
{
    public class BlockCompostPile : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null)
                return false;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCompostPile;
            if (be == null)
                return false;

            var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot == null || slot.Empty)
                return false;

            var stack = slot.Itemstack;
            var col = stack?.Collectible;
            if (col == null)
                return false;

            // Only accept food-like items (tighten/loosen this rule later)
            var nprops = col.NutritionProps;
            if (nprops == null)
                return false;

            // Determine category
            if (!TryMapFoodCategory(stack, out var cat))
                return false;

            int qty = stack.StackSize;

            if (!be.TryAdd(cat, qty, out int accepted) || accepted <= 0)
                return false;

            slot.TakeOut(accepted);
            slot.MarkDirty();

            // Optional: feedback text
            if (world.Side == EnumAppSide.Client
            &&  byPlayer is IClientPlayer cp
                )
                cp.ShowChatNotification(Lang.Get("Added {0}x {1} to compost pile", accepted, cat));

            return true;
        }

        private bool TryMapFoodCategory(ItemStack stack, out EnumFoodCategory cat)
        {
            cat = default;

            // 1) Prefer explicit attribute override (lets you fix edge cases without code)
            // Example in itemtype: attributes: { "compostCategory": "dairy" }
            var attr = stack.Collectible.Attributes?["compostCategory"].AsString(null);
            if (!string.IsNullOrEmpty(attr))
            {
                switch (attr)
                {
                    case "fruit": cat = EnumFoodCategory.Fruit; return true;
                    case "vegetable": cat = EnumFoodCategory.Vegetable; return true;
                    case "grain": cat = EnumFoodCategory.Grain; return true;
                    case "protein": cat = EnumFoodCategory.Protein; return true;
                    case "dairy": cat = EnumFoodCategory.Dairy; return true;
                }
                return false;
            }

            // 2) Fallback heuristics based on code path/name (last resort; brittle)
            var path = stack.Collectible.Code?.Path ?? "";
            if (path.Contains("cheese")
            ||  path.Contains("milk")
            ||  path.Contains("butter")
                )
            {
                cat = EnumFoodCategory.Dairy;
                return true;
            }

            // 3) Map via whatever NutritionProps exposes in your VS version.
            // Many versions have FoodCategory enums, but they don't always include "dairy".
            // So we only map common ones and reject unknowns.
            var np = stack.Collectible.NutritionProps;
            if (np == null)
                return false;

            // If your VS has np.FoodCategory (or similar), wire it here.
            // I’m leaving it commented because field names vary by version.
            //
            // switch (np.FoodCategory)
            // {
            //     case EnumFoodCategoryVS.Fruit: cat = EnumFoodCategory.Fruit; return true;
            //     case EnumFoodCategoryVS.Vegetable: cat = EnumFoodCategory.Vegetable; return true;
            //     case EnumFoodCategoryVS.Grain: cat = EnumFoodCategory.Grain; return true;
            //     case EnumFoodCategoryVS.Protein: cat = EnumFoodCategory.Protein; return true;
            // }

            return false;
        }
    }
}
