using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace MomentumGroves.Mobile.Views;

/// <summary>
/// Displays the Android camera solely to read a product barcode.
/// </summary>
public sealed class BarcodeScannerPage : ContentPage
{
    private readonly CameraBarcodeReaderView _reader;
    private bool _completed;

    /// <summary>
    /// Raised once a supported barcode has been detected.
    /// </summary>
    public event EventHandler<string>? BarcodeScanned;

    /// <summary>
    /// Initializes the barcode camera page.
    /// </summary>
    public BarcodeScannerPage()
    {
        Title = "Scan stregkode";
        BackgroundColor = Color.FromArgb("#101417");
        _reader = new CameraBarcodeReaderView
        {
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.OneDimensional,
                AutoRotate = true,
                Multiple = false
            },
            IsDetecting = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _reader.BarcodesDetected += OnBarcodesDetected;
        Content = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                _reader,
                new Label
                {
                    Text = "Hold stregkoden foran kameraet",
                    TextColor = Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(12)
                }
            }
        };
        ((Grid)Content).SetRow(((Grid)Content).Children[1], 1);
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs args)
    {
        var value = args.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value) || _completed)
        {
            return;
        }

        _completed = true;
        _reader.IsDetecting = false;
        MainThread.BeginInvokeOnMainThread(() => BarcodeScanned?.Invoke(this, value));
    }

    protected override void OnDisappearing()
    {
        _reader.IsDetecting = false;
        base.OnDisappearing();
    }
}