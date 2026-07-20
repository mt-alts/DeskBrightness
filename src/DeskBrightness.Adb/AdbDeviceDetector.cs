using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace DeskBrightness.Adb
{
    public sealed class AdbDeviceDetector
    {
        private readonly AdbCommandRunner _runner;

        public AdbDeviceDetector(AdbCommandRunner runner)
        {
            _runner = runner;
        }

        public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(
            CancellationToken cancellationToken = default
        )
        {
            return await _runner.GetDevicesAsync(cancellationToken);
        }

        public async Task<AdbDevice?> GetFirstReadyDeviceAsync(
            CancellationToken cancellationToken = default
        )
        {
            var devices = await GetDevicesAsync(cancellationToken);

            return devices.FirstOrDefault(x => x.State == AdbDeviceState.Device);
        }
    }
}
