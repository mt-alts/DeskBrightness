using System.Windows;
using DeskBrightness.Config;
using DeskBrightness.Views;

namespace DeskBrightness.Services
{
    public sealed class AdbConnectDialogService
    {
        public AdbTcpEndpoint? ShowDialog()
        {
            var dialog = new AdbConnectDialog
            {
                Owner = Application.Current.MainWindow,
                IpAddress = NetworkEndpointHelper.GetDefaultGatewayAddress() ?? string.Empty,
                Port = AppConfig.Network.AdbConnectDefaultPort,
            };

            return dialog.ShowDialog() == true
                ? new AdbTcpEndpoint(dialog.IpAddress, dialog.Port)
                : null;
        }

    }

    public sealed class AdbTcpEndpoint
    {
        public AdbTcpEndpoint(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }

        public int Port { get; }

        public string Serial => $"{Host}:{Port}";
    }
}
