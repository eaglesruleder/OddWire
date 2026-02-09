using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace OddWire.GameContent
{
    public class BlockEntityCompostPile : BlockEntity
    {
        public int FruitQty { get; private set; }
        public int VegetableQty { get; private set; }
        public int GrainQty { get; private set; }
        public int ProteinQty { get; private set; }
        public int DairyQty { get; private set; }

        public int Capacity { get; private set; } = 256;

        public int TotalQty => FruitQty + VegetableQty + GrainQty + ProteinQty + DairyQty;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            var attrs = Block?.Attributes;
            if (attrs != null)
                Capacity = attrs["capacity"].AsInt(Capacity);

            if (api.Side == EnumAppSide.Server)
                RegisterGameTickListener(OnEvery3Seconds, 3000);
        }

        private void OnEvery3Seconds(float dt)
        {
            // Placeholder: later convert categories into compost/sour/rot etc.
            // Intentionally empty for now.
        }

        public bool TryAdd(EnumFoodCategory cat, int qty, out int accepted)
        {
            accepted = 0;
            if (qty <= 0)
                return false;

            int room = Capacity - TotalQty;
            if (room <= 0)
                return false;

            accepted = qty > room ? room : qty;

            switch (cat)
            {
                case EnumFoodCategory.Fruit: FruitQty += accepted; break;
                case EnumFoodCategory.Vegetable: VegetableQty += accepted; break;
                case EnumFoodCategory.Grain: GrainQty += accepted; break;
                case EnumFoodCategory.Protein: ProteinQty += accepted; break;
                case EnumFoodCategory.Dairy: DairyQty += accepted; break;
                default:
                    // Unknown category -> reject (forces you to decide later)
                    accepted = 0;
                    return false;
            }

            MarkDirty(true);
            return accepted > 0;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            FruitQty = tree.GetInt("fruitQty");
            VegetableQty = tree.GetInt("vegetableQty");
            GrainQty = tree.GetInt("grainQty");
            ProteinQty = tree.GetInt("proteinQty");
            DairyQty = tree.GetInt("dairyQty");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("fruitQty", FruitQty);
            tree.SetInt("vegetableQty", VegetableQty);
            tree.SetInt("grainQty", GrainQty);
            tree.SetInt("proteinQty", ProteinQty);
            tree.SetInt("dairyQty", DairyQty);
        }
    }

    // Keep this local so you can map to VS categories however you like
    public enum EnumFoodCategory
    {
        Fruit,
        Vegetable,
        Grain,
        Protein,
        Dairy
    }
}
