using System;
using System.Collections.Generic;
using System.Text;

namespace DeskBrightness.Adb
{
    public sealed class AdbCommandResult
    {
        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }

        public bool IsSuccess => ExitCode == 0;

        public AdbCommandResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }
    }
}
