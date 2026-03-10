using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using SignalBench.SDK.Interfaces;

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

        var featureService = (App.Current as App)?.Services?.GetService<IFeatureService>();
        if (featureService != null)
        {
            var text = featureService.CurrentStatus switch
            {
                LicenseStatus.Pro => "PRO Edition",
                LicenseStatus.Free => "",
                LicenseStatus.Expired => "Trial Expired",
                LicenseStatus.Invalid => "Invalid License",
                _ => ""
            };
            
            LicenseStatusLabel.Text = text;
            LicenseStatusBorder.IsVisible = !string.IsNullOrEmpty(text);
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
