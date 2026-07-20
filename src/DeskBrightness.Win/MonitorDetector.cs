using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskBrightness.Win
{
    public sealed class MonitorDetector
    {
        public IReadOnlyList<IntPtr> GetDisplayMonitorHandles()
        {
            var handles = new List<IntPtr>();

            User32.EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (hMonitor, _, _, _) =>
                {
                    handles.Add(hMonitor);
                    return true;
                },
                IntPtr.Zero
            );

            return handles;
        }

        public bool HasPhysicalDdcMonitor()
        {
            foreach (var hMonitor in GetDisplayMonitorHandles())
            {
                if (!Dxva2.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count))
                    continue;

                if (count > 0)
                    return true;
            }

            return false;
        }
    }

    internal static class User32
    {
        public delegate bool MonitorEnumProc(
            IntPtr hMonitor,
            IntPtr hdcMonitor,
            IntPtr lprcMonitor,
            IntPtr dwData
        );

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            MonitorEnumProc lpfnEnum,
            IntPtr dwData
        );
    }
}
