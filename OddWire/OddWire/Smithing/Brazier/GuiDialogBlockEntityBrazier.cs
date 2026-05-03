using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#nullable disable

namespace OddWire.GameContent
{
    public class GuiDialogBlockEntityBrazier : GuiDialogBlockEntity
    {
        public class TreeKeys
        {
            public const string OUTPUT_TEXT          = "outputText";
            public const string INPUT_TYPE           = "inputType";
            public const string INPUT_ADDITIONAL_SLOTS = "inputAdditionalSlots";
            public const string FURNACE_TEMPERATURE  = "furnaceTemperature";
            public const string ORE_TEMPERATURE      = "oreTemperature";
            public const string FUEL_BURN_TIME       = "fuelBurnTime";
            public const string MAX_FUEL_BURN_TIME   = "maxFuelBurnTime";
            public const string ORE_COOKING_TIME     = "oreCookingTime";
            public const string MAX_ORE_COOKING_TIME = "maxOreCookingTime";

            public enum InputTypeEnum
                {None      = 0
                ,Item      = 1
                ,Container = 2
                ,Fuel      = 3
                ,Undefined = 99
                }
        }

        private const string SCKEY_SMALL_BLOCK_GUI  = "smallblockgui";
        private const string SCKEY_FUEL_SLOT        = "fuelslot";
        private const string SCKEY_ORE_SLOT         = "oreslot";
        private const string SCKEY_OUTPUT_SLOT      = "outputslot";
        private const string SCKEY_INGREDIENT_SLOTS = "ingredientSlots";
        private const string SCKEY_SYMBOL_DRAWER    = "symbolDrawer";
        private const string SCKEY_OUTPUT_TEXT      = "outputText";
        private const string SCKEY_FUEL_TEMP        = "fueltemp";
        private const string SCKEY_ORE_TEMP         = "oretemp";

        private string _prevStateKey;

        ElementBounds cookingSlotsSlotBounds;

        long lastRedrawMs;
        EnumPosFlag screenPos;

        protected override double FloatyDialogPosition => 0.6;
        protected override double FloatyDialogAlign    => 0.8;
        public    override double DrawOrder            => 0.2;

        public GuiDialogBlockEntityBrazier(string dlgTitle, InventoryBase Inventory, BlockPos bePos, SyncedTreeAttribute tree, ICoreClientAPI capi)
            : base(dlgTitle, Inventory, bePos, capi)
        {
            if (IsDuplicate)
                return;
            tree.OnModified.Add(new TreeModifiedListener { listener = OnAttributesModified });
            Attributes = tree;
        }

        private void OnTitleBarClose() => TryClose();

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnInventorySlotModified;

            screenPos = GetFreePos(SCKEY_SMALL_BLOCK_GUI);
            OccupyPos(SCKEY_SMALL_BLOCK_GUI, screenPos);
            SetupDialog();
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnInventorySlotModified;

            SingleComposer.GetSlotGrid(SCKEY_FUEL_SLOT).OnGuiClosed(capi);
            SingleComposer.GetSlotGrid(SCKEY_ORE_SLOT).OnGuiClosed(capi);
            SingleComposer.GetSlotGrid(SCKEY_OUTPUT_SLOT)?.OnGuiClosed(capi);
            SingleComposer.GetSlotGrid(SCKEY_INGREDIENT_SLOTS)?.OnGuiClosed(capi);

            base.OnGuiClosed();

            FreePos(SCKEY_SMALL_BLOCK_GUI, screenPos);
        }


        private void OnInventorySlotModified(int slotid) =>
            capi.Event.EnqueueMainThreadTask(SetupDialog, "setupbrazierdlg");

        void SetupDialog()
        {
            string outputText = Attributes.GetString(TreeKeys.OUTPUT_TEXT, "");
            TreeKeys.InputTypeEnum inputType = (TreeKeys.InputTypeEnum)Attributes.GetInt(TreeKeys.INPUT_TYPE);
            int qtyCookingSlots = Attributes.GetInt(TreeKeys.INPUT_ADDITIONAL_SLOTS);

            string stateKey = $"{outputText}{inputType}{qtyCookingSlots}";

            if (stateKey == _prevStateKey && SingleComposer != null)
            {
                SetupOutputText(outputText);
                SingleComposer.GetCustomDraw(SCKEY_SYMBOL_DRAWER).Redraw();
                _prevStateKey = stateKey;
                return;
            }
            _prevStateKey = stateKey;

            ElementBounds stoveBounds = ElementBounds.Fixed(0, 0, 210, 250);

            int qtyCookingSlotRows = qtyCookingSlots == 0 ? 0 : (qtyCookingSlots + 3) / 4;
            cookingSlotsSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30 + 45, 4, qtyCookingSlotRows);
            cookingSlotsSlotBounds.fixedHeight += 10;

            double top = cookingSlotsSlotBounds.fixedHeight + cookingSlotsSlotBounds.fixedY;

            ElementBounds inputSlotBounds  = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0,   top,       1, 1);
            ElementBounds fuelSlotBounds   = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0,   110 + top, 1, 1);
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 153, top,       1, 1);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(stoveBounds);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithFixedAlignmentOffset(IsRight(screenPos) ? -GuiStyle.DialogToScreenPadding : GuiStyle.DialogToScreenPadding, 0)
                .WithAlignment(IsRight(screenPos) ? EnumDialogArea.RightMiddle : EnumDialogArea.LeftMiddle);

            if (!capi.Settings.Bool["immersiveMouseMode"])
            {
                dialogBounds.fixedOffsetY += (stoveBounds.fixedHeight + 65 + (qtyCookingSlots > 0 ? 25 : 0)) * YOffsetMul(screenPos);
                dialogBounds.fixedOffsetX += (stoveBounds.fixedWidth + 10) * XOffsetMul(screenPos);
            }

            int[] cookingSlotIds = new int[qtyCookingSlots];
            for (int i = 0; i < qtyCookingSlots; i++)
                cookingSlotIds[i] = 3 + i;

            SingleComposer = capi.Gui
                .CreateCompo("blockentitystove" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddDynamicCustomDraw(stoveBounds, OnBgDraw, SCKEY_SYMBOL_DRAWER)
                    .AddDynamicText("", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 30, 210, 45), SCKEY_OUTPUT_TEXT)
                    .AddIf(qtyCookingSlots > 0)
                        .AddItemSlotGrid(Inventory, SendInvPacket, 4, cookingSlotIds, cookingSlotsSlotBounds, SCKEY_INGREDIENT_SLOTS)
                    .EndIf()
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new[] { 0 }, fuelSlotBounds,   SCKEY_FUEL_SLOT)
                    .AddDynamicText("", CairoFont.WhiteDetailText(), fuelSlotBounds.RightCopy(17, 16).WithFixedSize(60, 30), SCKEY_FUEL_TEMP)
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new[] { 1 }, inputSlotBounds,  SCKEY_ORE_SLOT)
                    .AddDynamicText("", CairoFont.WhiteDetailText(), inputSlotBounds.RightCopy(23, 16).WithFixedSize(60, 30), SCKEY_ORE_TEMP)
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new[] { 2 }, outputSlotBounds, SCKEY_OUTPUT_SLOT)
                .EndChildElements()
                .Compose();

            lastRedrawMs = capi.ElapsedMilliseconds;

            ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null
            &&  hoveredSlot.Inventory?.InventoryID == Inventory?.InventoryID
                )
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));

            SetupOutputText(outputText);
        }

        private void SetupOutputText(string text)
        {
            GuiElementDynamicText outputTextElem = SingleComposer.GetDynamicText(SCKEY_OUTPUT_TEXT);
            outputTextElem.Font.WithFontSize(14);
            outputTextElem.SetNewText(text, true);
            outputTextElem.Bounds.fixedOffsetY = 0;

            if (outputTextElem.QuantityTextLines > 2)
            {
                outputTextElem.Bounds.fixedOffsetY = -outputTextElem.Font.GetFontExtents().Height / RuntimeEnv.GUIScale * 0.65;
                outputTextElem.Font.WithFontSize(12);
                outputTextElem.RecomposeText();
            }

            outputTextElem.Bounds.CalcWorldBounds();
        }


        private void OnAttributesModified()
        {
            if (!IsOpened())
                return;

            OnTempAttributeChanged(Attributes.GetFloat(TreeKeys.FURNACE_TEMPERATURE), SCKEY_FUEL_TEMP);
            OnTempAttributeChanged(Attributes.GetFloat(TreeKeys.ORE_TEMPERATURE),     SCKEY_ORE_TEMP);

            if (capi.ElapsedMilliseconds - lastRedrawMs < 500)
                return;

            SingleComposer?.GetCustomDraw(SCKEY_SYMBOL_DRAWER).Redraw();
            lastRedrawMs = capi.ElapsedMilliseconds;
        }

        private void OnTempAttributeChanged(float temp, string textKey)
        {
            string textTemp = temp.ToString("#");
            if (temp > 0 && temp <= 20)
                textTemp = Lang.Get("Cold");
            else if (textTemp.Length > 0)
                textTemp += "°C";
            SingleComposer.GetDynamicText(textKey).SetNewText(textTemp);
        }

        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            float burnTime    = Attributes.GetFloat(TreeKeys.FUEL_BURN_TIME);
            float maxBurnTime = Attributes.GetFloat(TreeKeys.MAX_FUEL_BURN_TIME, 1);
            float cookTime    = Attributes.GetFloat(TreeKeys.ORE_COOKING_TIME);
            float maxCookTime = Attributes.GetFloat(TreeKeys.MAX_ORE_COOKING_TIME, 1);

            DrawFire(burnTime / maxBurnTime, ctx);
            DrawArrowRight(cookTime / maxCookTime, ctx);
        }

        private void DrawFire(float value, Context ctx)
        {
            double top = cookingSlotsSlotBounds.fixedHeight + cookingSlotsSlotBounds.fixedY;

            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(5), GuiElement.scaled(53 + top));
            m.Scale(GuiElement.scaled(0.25), GuiElement.scaled(0.25));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawFlame(ctx);

            double dy = 210 - 210 * value;
            ctx.Rectangle(0, dy, 200, 210 - dy);
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, GuiElement.scaled(250), 0, 0);
            gradient.AddColorStop(0, new Color(1, 1, 0, 1));
            gradient.AddColorStop(1, new Color(1, 0, 0, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawFlame(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }

        private void DrawArrowRight(float value, Context ctx)
        {
            double top = cookingSlotsSlotBounds.fixedHeight + cookingSlotsSlotBounds.fixedY;

            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(63), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            ctx.Rectangle(5, 0, 125 * value, 100);
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, 0, 200, 0);
            gradient.AddColorStop(0, new Color(0, 0.4, 0, 1));
            gradient.AddColorStop(1, new Color(0.2, 0.6, 0.2, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }

        private void SendInvPacket(object packet) =>
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, packet);
    }
}
