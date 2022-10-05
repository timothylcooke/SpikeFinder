using System.ComponentModel;

namespace SpikeFinder.Models
{
    public enum MeasureMode : byte
    {
        [Description("Phakic")] PHAKIC = 0,
        [Description("Aphakic")] APHAKIC = 1,
        [Description("Pseudophakic default IOL")] PSEUDOPHAKIC_DEFAULT = 2,
        [Description("Phakic, silicone oil-filled")] PHAKIC_SIL_OIL = 3,
        [Description("Aphakic, silicone oil-filled")] APHAKIC_SIL_OIL = 4,
        [Description("Pseudophakic standard IOL, silicone oil-filled")] PSEUDOPHAKIC_SIL_OIL = 5,
        [Description("Phakic IOL")] PHAKIC_IOL = 6,
        [Description("Pseudophakic PMMA")] PSEUDOPHAKIC_PMMA = 7,
        [Description("Pseudophakic acrylic")] PSEUDOPHAKIC_ACRYLIC = 8,
        [Description("Pseudophakic silicone")] PSEUDOPHAKIC_SILICONE = 9,
        [Description("LS900 Check Unit")] LS900_CHECK_UNIT = 10,
        [Description("Pseudophakic PMMA, silicone oil-filled")] PSEUDOPHAKIC_PMMA_SIL_OIL = 11,
        [Description("Pseudophakic acrylic, silicone oil-filled")] PSEUDOPHAKIC_ACRYLIC_SIL_OIL = 12,
        [Description("Pseudophakic silicone, silicone oil-filled")] PSEUDOPHAKIC_SILICONE_SIL_OIL = 13,
        [Description("Short Eye (Phakic)")] SHORT_EYE_PHAKIC = 14,
    }
}
