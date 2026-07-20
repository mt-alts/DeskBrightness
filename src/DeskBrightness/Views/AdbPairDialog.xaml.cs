using System.Net;
using System.Windows;

namespace DeskBrightness.Views
{
    public partial class AdbPairDialog : Window
    {
        public AdbPairDialog()
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

        public string PairingCode
        {
            get => PairingCodeTextBox.Text.Trim();
            set => PairingCodeTextBox.Text = value;
        }

        private void TryPair_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private bool ValidateInputs()
        {
            ErrorTextBlock.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(IpAddress))
            {
                ErrorTextBlock.Text = TryFindResource("InvalidIpError") as string ?? "IP adresi gerekli.";
                return false;
            }

            if (!IPAddress.TryParse(IpAddress, out _))
            {
                ErrorTextBlock.Text = TryFindResource("InvalidIpError") as string ?? "Geçerli bir IP adresi girin.";
                return false;
            }

            if (Port < 1 || Port > 65535)
            {
                ErrorTextBlock.Text = TryFindResource("InvalidPortError") as string ?? "Port 1 ile 65535 arasında olmalı.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PairingCode))
            {
                ErrorTextBlock.Text = TryFindResource("PairingCodeRequired") as string ?? "Eşleştirme kodu gerekli.";
                return false;
            }

            return true;
        }
    }
}
