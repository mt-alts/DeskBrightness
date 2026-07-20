using System;
using System.Collections.Generic;
using System.Text;
using DeskBrightness.Core.Profiles;

namespace DeskBrightness.Core.Brightness
{
    public sealed class DefaultLuxToBrightnessMapper : ILuxToBrightnessMapper
    {
        private static readonly (double Lux, byte Percent)[] DefaultCurve = new (double Lux, byte Percent)[]
        {
            (0, 0),
            (10, 3),
            (25, 10),
            (50, 20),
            (100, 35),
            (200, 50),
            (400, 65),
            (700, 78),
            (1200, 88),
            (2000, 95),
            (5000, 100),
        };

        public BrightnessLevel Map(double lux, BrightnessProfile profile)
        {
            if (lux < 0)
                lux = 0;

            var percent = Interpolate(lux, DefaultCurve);

            percent = Clamp(percent, profile.MinimumBrightness, profile.MaximumBrightness);

            return BrightnessLevel.FromPercent(percent);
        }

        private static byte Interpolate(double lux, (double Lux, byte Percent)[] points)
        {
            if (lux <= points[0].Lux)
                return points[0].Percent;

            for (var i = 1; i < points.Length; i++)
            {
                var prev = points[i - 1];
                var next = points[i];

                if (lux > next.Lux)
                    continue;

                var ratio = (lux - prev.Lux) / (next.Lux - prev.Lux);
                var value = prev.Percent + ((next.Percent - prev.Percent) * ratio);

                return (byte)Math.Round(value);
            }

            return points[^1].Percent;
        }

        private static byte Clamp(byte value, byte min, byte max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
