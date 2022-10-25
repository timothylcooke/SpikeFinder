using SpikeFinder.Models;
using System;

namespace SpikeFinder.Attributes
{
    public class OcularMaterialAttribute : Attribute
    {
        public LensMaterial LensMaterial { get; }
        public VitreousMaterial VitreousMaterial { get; }

        public OcularMaterialAttribute(LensMaterial lensMaterial, VitreousMaterial vitreousMaterial)
        {
            LensMaterial = lensMaterial;
            VitreousMaterial = vitreousMaterial;
        }
    }
}
