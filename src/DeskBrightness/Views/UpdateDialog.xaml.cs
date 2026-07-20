using System.Windows;

namespace DeskBrightness.Views
{
    public partial class UpdateDialog : Window
    {
        public bool DownloadConfirmed { get; private set; }

        public UpdateDialog()
        {
            InitializeComponent();
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadConfirmed = true;
            DialogResult = true;
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}