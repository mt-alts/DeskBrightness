using System;
using System.Collections.Generic;
using System.Text;

namespace DeskBrightness.Core.Brightness
{
    public struct BrightnessLevel
    {
        public byte Percent { get; }

        public BrightnessLevel(byte percent)
        {
            if (percent > 100)
                throw new ArgumentOutOfRangeException(nameof(percent));

            Percent = percent;
        }

        public static BrightnessLevel FromPercent(byte percent)
        {
            return new BrightnessLevel(percent);
        }

        public override string ToString()
        {
            return $"{Percent}%";
        }
    }
}
