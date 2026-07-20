using System;
using System.Collections.Generic;
using System.Text;

namespace DeskBrightness.Core.Filtering
{
    public sealed class ThresholdLuxFilter
    {
        private double? _lastAcceptedLux;

        public double Threshold { get; }

        public ThresholdLuxFilter(double threshold)
        {
            if (threshold < 0)
                throw new ArgumentOutOfRangeException(nameof(threshold));

            Threshold = threshold;
        }

        public bool ShouldAccept(double lux)
        {
            if (_lastAcceptedLux is null)
            {
                _lastAcceptedLux = lux;
                return true;
            }

            var difference = Math.Abs(lux - _lastAcceptedLux.Value);

            if (difference < Threshold)
                return false;

            _lastAcceptedLux = lux;
            return true;
        }

        public void Reset()
        {
            _lastAcceptedLux = null;
        }
    }
}
