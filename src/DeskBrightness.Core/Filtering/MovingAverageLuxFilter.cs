using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeskBrightness.Core.Filtering
{
    public sealed class MovingAverageLuxFilter
    {
        private readonly Queue<double> _samples = new();

        public int WindowSize { get; }

        public MovingAverageLuxFilter(int windowSize)
        {
            if (windowSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowSize));

            WindowSize = windowSize;
        }

        public double Add(double lux)
        {
            _samples.Enqueue(lux);

            while (_samples.Count > WindowSize)
                _samples.Dequeue();

            return _samples.Average();
        }

        public void Reset()
        {
            _samples.Clear();
        }
    }
}
