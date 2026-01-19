using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace OddWire.VintageStory.API.Common
{
    public static class ShapeExtensions
    {
        public static string Path(this CompositeShape shape)
        {
            string code = shape.Base;
            int index = code.IndexOf(':');
            if (index < 0)
                return code;

            string nspace = code.Substring(0, index + 1);
            string path = code.Substring(index + 1);

            if (!path.StartsWith("shapes/", StringComparison.Ordinal))
                path = "shapes/" + path;

            return nspace + path;
        }
    }
}
