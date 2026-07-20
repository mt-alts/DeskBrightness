using System;
using System.Collections.Generic;
using System.Text;

namespace DeskBrightness.Core.Sensors
{
    public sealed class LuxSample
    {
        public double Lux { get; }
        public DateTimeOffset Timestamp { get; }

        public LuxSample(double lux, DateTimeOffset timestamp)
        {
            Lux = lux;
            Timestamp = timestamp;
        }
    }
}
