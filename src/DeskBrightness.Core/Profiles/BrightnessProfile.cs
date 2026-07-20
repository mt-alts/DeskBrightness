using System;
using System.Collections.Generic;
using System.Text;

namespace DeskBrightness.Core.Profiles
{
    public sealed class BrightnessProfile
    {
        public byte MinimumBrightness { get; set; } = 15;

        public byte MaximumBrightness { get; set; } = 90;

        public double LuxThreshold { get; set; } = 5;

        public byte BrightnessStepThreshold { get; set; } = 5;

        public int SampleIntervalMs { get; set; } = 2000;

        public int SmoothingWindowSize { get; set; } = 5;

        public TimeSpan MinimumApplyInterval { get; set; } = TimeSpan.FromSeconds(3);

        public double SensorMaxLux { get; set; } = 65535;

        public double LogCurveExponent { get; set; } = 1.0;

        public double LowPassAlpha { get; set; } = 0.3;

        public double DarkThresholdLux { get; set; } = 10;

        public void Validate()
        {
            if (MinimumBrightness > 100)
                throw new InvalidOperationException("MinimumBrightness cannot exceed 100.");

            if (MaximumBrightness > 100)
                throw new InvalidOperationException("MaximumBrightness cannot exceed 100.");

            if (MinimumBrightness > MaximumBrightness)
                throw new InvalidOperationException(
                    "MinimumBrightness cannot be greater than MaximumBrightness."
                );

            if (LuxThreshold < 0)
                throw new InvalidOperationException("LuxThreshold cannot be negative.");

            if (SampleIntervalMs <= 0)
                throw new InvalidOperationException("SampleIntervalMs must be greater than zero.");

            if (SmoothingWindowSize <= 0)
                throw new InvalidOperationException(
                    "SmoothingWindowSize must be greater than zero."
                );
        }
    }
}
