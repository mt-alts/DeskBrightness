using System.Net;
using System.Windows;

namespace DeskBrightness.Views
{
    public partial class AdbConnectDialog : Window
    {
        public AdbConnectDialog()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (Owner is not null)
                {
                    MaxWidth = Owner.Width + Owner.Margin.Left + Owner.Margin.Right;
                    MaxHeight = Owner.Height;
                }
                IpAddressTextBox.Focus();
                IpAddressTextBox.SelectAll();
            };
        }

        public string IpAddress
        {
            get => IpAddressTextBox.Text.Trim();
            set => IpAddressTextBox.Text = value;
        }

        public int Port
        {
            get
            {
                return int.TryParse(PortTextBox.Text.Trim(), out var port) ? port : 0;
            }
            set => PortTextBox.Text = value.ToString();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Text = string.Empty;

            if (!IPAddress.TryParse(IpAddress, out _))
            {
                ErrorTextBlock.Text = TryFindResource("InvalidIpError") as string ?? "Geçerli bir IP adresi girin.";
                IpAddressTextBox.Focus();
                IpAddressTextBox.SelectAll();
                return;
            }

            if (Port < 1 || Port > 65535)
            {
                ErrorTextBlock.Text = TryFindResource("InvalidPortError") as string ?? "Port 1 ile 65535 arasında olmalı.";
                PortTextBox.Focus();
                PortTextBox.SelectAll();
                return;
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
