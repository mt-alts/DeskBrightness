using System.Windows;
using DeskBrightness.Config;
using DeskBrightness.Views;

namespace DeskBrightness.Services
{
    public sealed class AdbPairDialogService
    {
        public AdbPairEndpoint? ShowDialog()
        {
            var dialog = new AdbPairDialog
            {
                Owner = Application.Current.MainWindow,
                IpAddress = NetworkEndpointHelper.GetDefaultGatewayAddress() ?? string.Empty,
                Port = AppConfig.Network.AdbPairDefaultPort,
                PairingCode = string.Empty,
            };

            return dialog.ShowDialog() == true
                ? new AdbPairEndpoint(dialog.IpAddress, dialog.Port, dialog.PairingCode)
                : null;
        }
    }

    public sealed class AdbPairEndpoint
    {
        public AdbPairEndpoint(string host, int port, string pairingCode)
        {
            Host = host;
            Port = port;
            PairingCode = pairingCode;
        }

        public string Host { get; }

        public int Port { get; }

        public string PairingCode { get; }

        public string Serial => $"{Host}:{Port}";
    }
}
