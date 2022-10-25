using SpikeFinder.Attributes;
using System.ComponentModel;
using static SpikeFinder.Models.LensMaterial;
using static SpikeFinder.Models.VitreousMaterial;

namespace SpikeFinder.Models
{
    public enum MeasureMode : byte
    {
        [OcularMaterial(Phakic              , Vitreous   ), Description("Phakic"                                        )] PHAKIC                        =  0,
        [OcularMaterial(Aphakic             , Vitreous   ), Description("Aphakic"                                       )] APHAKIC                       =  1,
        [OcularMaterial(PseudophakicDefault , Vitreous   ), Description("Pseudophakic default IOL"                      )] PSEUDOPHAKIC_DEFAULT          =  2,
        [OcularMaterial(Phakic              , SiliconeOil), Description("Phakic, silicone oil-filled"                   )] PHAKIC_SIL_OIL                =  3,
        [OcularMaterial(Aphakic             , SiliconeOil), Description("Aphakic, silicone oil-filled"                  )] APHAKIC_SIL_OIL               =  4,
        [OcularMaterial(PseudophakicDefault , SiliconeOil), Description("Pseudophakic standard IOL, silicone oil-filled")] PSEUDOPHAKIC_SIL_OIL          =  5,
        [OcularMaterial(Phakic              , Vitreous   ), Description("Phakic IOL"                                    )] PHAKIC_IOL                    =  6, // This mode should not be used.
        [OcularMaterial(PseudophakicPMMA    , Vitreous   ), Description("Pseudophakic PMMA"                             )] PSEUDOPHAKIC_PMMA             =  7,
        [OcularMaterial(PseudophakicAcrylic , Vitreous   ), Description("Pseudophakic acrylic"                          )] PSEUDOPHAKIC_ACRYLIC          =  8,
        [OcularMaterial(PseudophakicSilicone, Vitreous   ), Description("Pseudophakic silicone"                         )] PSEUDOPHAKIC_SILICONE         =  9,
        [OcularMaterial(Phakic              , Vitreous   ), Description("LS900 Check Unit"                              )] LS900_CHECK_UNIT              = 10, // We'll call it phakic/vitreous. In reality, this mode should not be used. If it is used, its RIs (and therefore segment values) are going to be very different from the Lenstar's values.
        [OcularMaterial(PseudophakicPMMA    , SiliconeOil), Description("Pseudophakic PMMA, silicone oil-filled"        )] PSEUDOPHAKIC_PMMA_SIL_OIL     = 11,
        [OcularMaterial(PseudophakicAcrylic , SiliconeOil), Description("Pseudophakic acrylic, silicone oil-filled"     )] PSEUDOPHAKIC_ACRYLIC_SIL_OIL  = 12,
        [OcularMaterial(PseudophakicSilicone, SiliconeOil), Description("Pseudophakic silicone, silicone oil-filled"    )] PSEUDOPHAKIC_SILICONE_SIL_OIL = 13,
        [OcularMaterial(Phakic              , Vitreous   ), Description("Short Eye (Phakic)"                            )] SHORT_EYE_PHAKIC              = 14,
    }
}
