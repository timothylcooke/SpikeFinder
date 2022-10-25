using HexInnovation;
using Splat;
using System;
using System.Globalization;

namespace SpikeFinder.RefractiveIndices
{
    public class CustomRefractiveIndex
    {
        private static readonly MathConverter _mathConverter = new();
        private static readonly MemoizingMRUCache<string, CustomRefractiveIndex> _customRefractiveIndices = new((equation, _) => new CustomRefractiveIndex(equation), 20);

        public static CustomRefractiveIndex FromEquation(string equation) => _customRefractiveIndices.Get(equation);


        public string Equation { get; }

        private MemoizingMRUCache<double, double> _cache;

        private CustomRefractiveIndex(string equation)
        {
            Equation = equation;
            _cache = new((value, _) =>
            {
                try
                {
                    return (double)_mathConverter.Convert(value, typeof(double), equation, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    App.SpikeFinderMainWindow.NotifyException(ex);
                    throw;
                }
            }, 20);
        }

        public double ComputeRefractiveIndex(double wavelength) => _cache.Get(wavelength);
    }
}
