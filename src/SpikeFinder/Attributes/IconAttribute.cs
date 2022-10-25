using System;

namespace SpikeFinder.Attributes
{
    public class IconAttribute : Attribute
    {
        public string PathData { get; }
        public IconAttribute(string pathData)
        {
            PathData = pathData;
        }
    }
}
