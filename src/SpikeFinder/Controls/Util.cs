using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SpikeFinder.Controls
{
    static class Util
    {
        public static IEnumerable<DependencyObject> GetVisualTreeParents(this DependencyObject o)
        {
            while ((o = VisualTreeHelper.GetParent(o)) != null)
            {
                yield return o;
            }
        }
    }
}
