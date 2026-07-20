using System;
using System.Collections.Generic;
using System.Text;
using DeskBrightness.Core.Brightness;

namespace DeskBrightness.Core.Filtering
{
    public sealed class HysteresisFilter
    {
        private BrightnessLevel? _lastAcceptedLevel;

        public byte StepThreshold { get; }

        public HysteresisFilter(byte stepThreshold)
        {
            StepThreshold = stepThreshold;
        }

        public bool ShouldApply(BrightnessLevel level)
        {
            if (_lastAcceptedLevel is null)
            {
                _lastAcceptedLevel = level;
                return true;
            }

            var difference = Math.Abs(level.Percent - _lastAcceptedLevel.Value.Percent);

            if (difference < StepThreshold)
                return false;

            _lastAcceptedLevel = level;
            return true;
        }

        public void Reset()
        {
            _lastAcceptedLevel = null;
        }
    }
}
