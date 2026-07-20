using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeskBrightness.Core.Brightness
{
    public interface IBrightnessController
    {
        Task SetBrightnessAsync(byte percent, CancellationToken cancellationToken = default);
    }
}
