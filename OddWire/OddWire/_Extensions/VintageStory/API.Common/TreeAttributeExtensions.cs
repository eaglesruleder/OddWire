using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace OddWire.VintageStory.API.Common
{
    public static class TreeAttributeExtensions
    {
        public static void SetCombustibleProps(this ITreeAttribute tree, string key, CombustibleProperties props)
        {
            if(props.BurnDuration != 0)    tree.SetFloat($"{key}.BurnDuration", props.BurnDuration);
            if(props.BurnTemperature != 0) tree.SetInt($"{key}.BurnTemperature", props.BurnTemperature);
            if(props.HeatResistance != 0)  tree.SetInt($"{key}.HeatResistance", props.HeatResistance);
            if(props.MeltingDuration != 0) tree.SetFloat($"{key}.MeltingDuration", props.MeltingDuration);
            if(props.MeltingPoint != 0)    tree.SetInt($"{key}.MeltingPoint", props.MeltingPoint);
            if(props.SmokeLevel != 0)      tree.SetFloat($"{key}.SmokeLevel", props.SmokeLevel);
            if(props.SmeltedRatio != 0)    tree.SetInt($"{key}.SmeltedRatio", props.SmeltedRatio);
            if(props.RequiresContainer)    tree.SetBool($"{key}.RequiresContainer", props.RequiresContainer);
            if(props.SmeltingType != 0)    tree.SetInt($"{key}.SmeltingType", (int)props.SmeltingType);
            if(props.MaxTemperature != 0)  tree.SetInt($"{key}.MaxTemperature", props.MaxTemperature);
        //  if (this.SmeltedStack != null)
        //      props.SmeltedStack = this.SmeltedStack.Clone();
        }
        
        public static CombustibleProperties GetCombustibleProps(this ITreeAttribute tree, string key)
        {
            CombustibleProperties props = new CombustibleProperties();
            props.BurnDuration = tree.GetFloat($"{key}.BurnDuration");
            props.BurnTemperature = tree.GetInt($"{key}.BurnTemperature");
            props.HeatResistance = tree.GetInt($"{key}.HeatResistance");
            props.MeltingDuration = tree.GetFloat($"{key}.MeltingDuration");
            props.MeltingPoint = tree.GetInt($"{key}.MeltingPoint");
            props.SmokeLevel = tree.GetFloat($"{key}.SmokeLevel");
            props.SmeltedRatio = tree.GetInt($"{key}.SmeltedRatio");
            props.RequiresContainer = tree.GetBool($"{key}.RequiresContainer");
            props.SmeltingType = (EnumSmeltType)tree.GetInt($"{key}.SmeltedRatio");
            props.MaxTemperature = tree.GetInt($"{key}.MaxTemperature");
        //  if (props.SmeltedStack != null)
        //      props.SmeltedStack = props.SmeltedStack.Clone();
            return props;
        }
    }    
}
