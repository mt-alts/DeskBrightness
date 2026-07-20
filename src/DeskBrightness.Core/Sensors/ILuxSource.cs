using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeskBrightness.Core.Sensors
{
    public interface ILuxSource
    {
        event EventHandler<LuxSample>? SampleReceived;

        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
