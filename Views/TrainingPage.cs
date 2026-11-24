namespace MomentumGroves.Mobile.Views;

public sealed class TrainingPage : ContentPage
{
    private readonly Uri _address;
    private readonly WebView _training = new() { IsVisible = false };
    private readonly Label _message = new()
    {
        TextColor = Color.FromArgb("#eef2f1"),
        HorizontalTextAlignment = TextAlignment.Center
    };
    private readonly Button _retry = new()
    {
        Text = "Try again",
        BackgroundColor = Color.FromArgb("#a6da7e"),
        TextColor = Color.FromArgb("#101417")
    };
    private readonly VerticalStackLayout _connectionStatus = new()
    {
        Padding = 24,
        Spacing = 12,
        VerticalOptions = LayoutOptions.Center
    };

    public TrainingPage(Uri address)
    {
        _address = address;
        Title = "Training";
        BackgroundColor = Color.FromArgb("#101417");

        _training.Navigating += (_, args) =>
        {
            args.Cancel = !Uri.TryCreate(args.Url, UriKind.Absolute, out var target) ||
                          !_address.Equals(target);
        };
        _training.Navigated += (_, args) =>
        {
            var success = args.Result == WebNavigationResult.Success;
            _training.IsVisible = success;
            _connectionStatus.IsVisible = !success;
            _retry.IsVisible = !success;
            _message.Text = "Couldn't open Training. Check your connection to the PC and its HTTPS certificate.";
        };
        _retry.Clicked += (_, _) => LoadTraining();

        _connectionStatus.Children.Add(_message);
        _connectionStatus.Children.Add(_retry);
        Content = new Grid { Children = { _training, _connectionStatus } };
        LoadTraining();
    }

    private void LoadTraining()
    {
        _message.Text = "Loading training programmes…";
        _retry.IsVisible = false;
        _connectionStatus.IsVisible = true;
        _training.Source = new UrlWebViewSource { Url = _address.AbsoluteUri };
    }
}
