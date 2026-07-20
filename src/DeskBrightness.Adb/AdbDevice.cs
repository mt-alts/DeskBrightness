using System;
using System.Collections.Generic;
using System.Text;

namespace DeskBrightness.Adb
{
    public sealed class AdbDevice
    {
        public string Serial { get; }
        public AdbDeviceState State { get; }
        public string? ModelName { get; }
        public bool IsEmulator { get; }
        public bool IsPhysicalDevice => State == AdbDeviceState.Device && !IsEmulator;

        public AdbDevice(
            string serial,
            AdbDeviceState state,
            string? modelName = null,
            bool isEmulator = false
        )
        {
            Serial = serial;
            State = state;
            ModelName = modelName;
            IsEmulator = isEmulator;
        }
    }

    public enum AdbDeviceState
    {
        Unknown,
        Device,
        Offline,
        Unauthorized,
    }
}
