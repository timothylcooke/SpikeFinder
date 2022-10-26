using System;

namespace SpikeFinder.Models
{
    public record ValueWithStandardDeviation(double Value, double? StandardDeviation) : IComparable<ValueWithStandardDeviation>
    {
        public static ValueWithStandardDeviation FromValues(double value, double? standardDeviation)
        {
            return new ValueWithStandardDeviation(value, standardDeviation);
        }
        public static ValueWithStandardDeviation? FromValues(double? value, double? standardDeviation)
        {
            if (value.HasValue)
            {
                return FromValues(value.Value, standardDeviation);
            }
            else if (standardDeviation.HasValue)
            {
                throw new ArgumentException("If the value is null, the standard deviation must also be null.", nameof(standardDeviation));
            }
            else
            {
                return null;
            }
        }
        public int CompareTo(ValueWithStandardDeviation? other)
        {
            return Value.CompareTo(other?.Value);
        }

        public override string ToString()
        {
            return $"{Value}";
            //return StandardDeviation.HasValue ? $"{Value} ± {StandardDeviation}" : $"{Value}";
        }
    }
}
