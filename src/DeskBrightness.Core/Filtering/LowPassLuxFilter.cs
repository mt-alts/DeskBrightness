using System;

namespace DeskBrightness.Core.Filtering
{
    public sealed class LowPassLuxFilter
    {
        private double? _smoothed;

        public double Alpha { get; }

        public LowPassLuxFilter(double alpha)
        {
            if (alpha < 0.01 || alpha > 1.0)
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be between 0.01 and 1.0");

            Alpha = alpha;
        }

        public double Add(double lux)
        {
            if (_smoothed is null)
            {
                _smoothed = lux;
                return lux;
            }

            _smoothed = (Alpha * lux) + ((1.0 - Alpha) * _smoothed.Value);
            return _smoothed.Value;
        }

        public void Reset()
        {
            _smoothed = null;
        }
    }
}
