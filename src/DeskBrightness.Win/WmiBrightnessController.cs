using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using DeskBrightness.Core.Brightness;

namespace DeskBrightness.Win
{
    public sealed class WmiBrightnessController : IBrightnessController
    {
        public Task SetBrightnessAsync(byte percent, CancellationToken cancellationToken = default)
        {
            percent = Clamp(percent);

            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                using var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    "SELECT * FROM WmiMonitorBrightnessMethods"
                );

                var appliedCount = 0;

                foreach (ManagementObject method in searcher.Get())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    method.InvokeMethod("WmiSetBrightness", new object[] { 1u, percent });
                    appliedCount++;
                }

                if (appliedCount == 0)
                    throw new InvalidOperationException("No WMI brightness methods were found.");
            },
                cancellationToken
            );
        }

        public bool IsSupported()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    "SELECT * FROM WmiMonitorBrightnessMethods"
                );

                return searcher.Get().Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static byte Clamp(byte value)
        {
            if (value < 1)
                return 1;

            if (value > 100)
                return 100;

            return value;
        }
    }
}
