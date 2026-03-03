using Avalonia.Controls;

namespace SignalBench.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        
        VersionText.Text = $"Version {Core.AppInfo.Version}";
        TaglineText.Text = Core.AppInfo.Tagline;
        DescriptionText.Text = Core.AppInfo.Description;
        CopyrightText.Text = Core.AppInfo.Copyright;
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
