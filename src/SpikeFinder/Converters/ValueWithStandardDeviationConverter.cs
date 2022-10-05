using SpikeFinder.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SpikeFinder.Converters
{
    public class ValueWithStandardDeviationConverter : IValueConverter
    {
        public object? Convert(object? value, Type? targetType, object? parameter, CultureInfo? culture)
        {
            if (value is ValueWithStandardDeviation valueSd)
            {
                var multiplier = parameter is string p && !p.Contains(".") ? 1000 : 1;

                if (valueSd.StandardDeviation.HasValue)
                {
                    return string.Format($"{{0:{parameter ?? "0.00"}}} ± {{1:{parameter ?? "0.00"}}}", valueSd.Value * multiplier, valueSd.StandardDeviation * multiplier);
                }
                else
                {
                    return string.Format($"{{0:{parameter ?? "0.00"}}}", valueSd.Value * multiplier);
                }
            }

            return value;
        }
        public object ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo? culture)
        {
            throw new NotSupportedException();
        }
    }
}
