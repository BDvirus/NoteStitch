using Microsoft.UI.Xaml.Controls;

namespace NoteStitch;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();

        VersionLabel.Text = $"v{UpdateChecker.CurrentVersion.ToString(3)}";
    }

    private void OnCloseClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        Frame.GoBack();
}
