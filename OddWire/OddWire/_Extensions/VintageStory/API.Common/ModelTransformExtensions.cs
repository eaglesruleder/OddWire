using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace OddWire.VintageStory.API.Common
{
    public static class ModelTransformExtensions
    {
        public static string ToKey(this ModelTransform t) =>
            $"({t?.Origin.X:F2},{t?.Origin.Y:F2},{t?.Origin.Z:F2}::{t?.Translation.X:F2},{t?.Translation.Y:F2},{t?.Translation.Z:F2}::{t?.Rotation.X:F2},{t?.Rotation.Y:F2},{t?.Rotation.Z:F2}::{t?.ScaleXYZ.X:F2},{t?.ScaleXYZ.Y:F2},{t?.ScaleXYZ.Z:F2})";
    }
}
