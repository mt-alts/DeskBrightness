using System;
using System.Windows;
using System.Windows.Media;
using DeskBrightness.Models;

namespace DeskBrightness.Controls
{
    public partial class InfoBar : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty SeverityProperty =
            DependencyProperty.Register(nameof(Severity), typeof(InfoBarSeverity), typeof(InfoBar),
                new PropertyMetadata(InfoBarSeverity.Informational, OnSeverityChanged));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(InfoBar),
                new PropertyMetadata(string.Empty, OnTitleChanged));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(InfoBar),
                new PropertyMetadata(string.Empty, OnMessageChanged));

        public InfoBarSeverity Severity
        {
            get => (InfoBarSeverity)GetValue(SeverityProperty);
            set => SetValue(SeverityProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public event EventHandler? Dismissed;

        public InfoBar()
        {
            InitializeComponent();
            ApplySeverity(InfoBarSeverity.Informational);
        }

        private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBar bar && e.NewValue is InfoBarSeverity severity)
                bar.ApplySeverity(severity);
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBar bar)
                bar.TitleText.Text = e.NewValue as string ?? string.Empty;
        }

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoBar bar)
                bar.MessageText.Text = e.NewValue as string ?? string.Empty;
        }

        private void ApplySeverity(InfoBarSeverity severity)
        {
            switch (severity)
            {
                case InfoBarSeverity.Success:
                    AccentBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    IconText.Text = "\uE930";
                    IconText.Foreground = Brushes.White;
                    break;

                case InfoBarSeverity.Warning:
                    AccentBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                    IconText.Text = "\uE7BA";
                    IconText.Foreground = Brushes.White;
                    break;

                case InfoBarSeverity.Error:
                    AccentBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                    IconText.Text = "\uE711";
                    IconText.Foreground = Brushes.White;
                    break;

                default:
                    AccentBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));
                    IconText.Text = "\uE946";
                    IconText.Foreground = Brushes.White;
                    break;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
        }
    }
}