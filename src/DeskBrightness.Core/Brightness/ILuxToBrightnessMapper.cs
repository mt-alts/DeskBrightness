using System;
using System.Collections.Generic;
using System.Text;
using DeskBrightness.Core.Profiles;

namespace DeskBrightness.Core.Brightness
{
    public interface ILuxToBrightnessMapper
    {
        BrightnessLevel Map(double lux, BrightnessProfile profile);
    }
}
