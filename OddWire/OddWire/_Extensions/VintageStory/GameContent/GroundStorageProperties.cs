using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace OddWire.VintageStory.GameContent
{
    public static class GroundStoragePropertiesExtensions
    {
        public static void SetGroundStorageProps(this ITreeAttribute tree, string key, GroundStorageProperties props)
        {
            if (props is null) return;
            if (props.Layout != 0)                    tree.SetInt($"{key}.Layout", (int)props.Layout);
            if (props.WallOffY != 0)                  tree.SetInt($"{key}.WallOffY", props.WallOffY);
        //  PlaceRemoveSound
            if (props.RandomizeSoundPitch)            tree.SetBool($"{key}.RandomizeSoundPitch", props.RandomizeSoundPitch);
            if (props.RandomizeCenterRotation)        tree.SetBool($"{key}.RandomizeCenterRotation", props.RandomizeCenterRotation);
        //  StackingModel
            if(props.ModelItemsToStackSizeRatio != 0) tree.SetFloat($"{key}.ModelItemsToStackSizeRatio", props.ModelItemsToStackSizeRatio);
        //  StackingTextures
            if (props.MaxStackingHeight != 0)         tree.SetInt($"{key}.MaxStackingHeight", props.MaxStackingHeight);
            if (props.StackingCapacity != 0)          tree.SetInt($"{key}.StackingCapacity", props.StackingCapacity);
            if (props.TransferQuantity != 0)          tree.SetInt($"{key}.TransferQuantity", props.TransferQuantity);
            if (props.BulkTransferQuantity != 0)      tree.SetInt($"{key}.BulkTransferQuantity", props.BulkTransferQuantity);
            if (props.CtrlKey)                        tree.SetBool($"{key}.CtrlKey", props.CtrlKey);
            if (props.UpSolid)                        tree.SetBool($"{key}.UpSolid", props.UpSolid);
        //  CollisionBox
        //  SelectionBox
            if (props.CbScaleYByLayer != 0)           tree.SetFloat($"{key}.CbScaleYByLayer", props.CbScaleYByLayer);
            if (props.MaxFireable != 0)               tree.SetInt($"{key}.MaxFireable", props.MaxFireable);
        }
        
        public static GroundStorageProperties GetGroundStorageProps(this ITreeAttribute tree, string key)
        {
            GroundStorageProperties props = new GroundStorageProperties();
            props.Layout                     = (EnumGroundStorageLayout)tree.GetInt($"{key}.Layout");
            props.WallOffY                   = tree.GetInt($"{key}.WallOffY");
        //  PlaceRemoveSound
            props.RandomizeSoundPitch        = tree.GetBool($"{key}.RandomizeSoundPitch");
            props.RandomizeCenterRotation    = tree.GetBool($"{key}.RandomizeCenterRotation");
        //  StackingModel
            props.ModelItemsToStackSizeRatio = tree.GetFloat($"{key}.ModelItemsToStackSizeRatio");
        //  StackingTextures
            props.MaxStackingHeight          = tree.GetInt($"{key}.MaxStackingHeight");    
            props.StackingCapacity           = tree.GetInt($"{key}.StackingCapacity");
            props.TransferQuantity           = tree.GetInt($"{key}.TransferQuantity");
            props.BulkTransferQuantity       = tree.GetInt($"{key}.BulkTransferQuantity");
            props.CtrlKey                    = tree.GetBool($"{key}.CtrlKey");
            props.UpSolid                    = tree.GetBool($"{key}.UpSolid");
        //  CollisionBox
        //  SelectionBox
            props.CbScaleYByLayer            = tree.GetFloat($"{key}.CbScaleYByLayer");
            props.MaxFireable                = tree.GetInt($"{key}.MaxFireable");
            return props;
        }
    }
}
