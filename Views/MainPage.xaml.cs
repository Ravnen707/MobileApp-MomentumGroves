using System.Globalization;
using MomentumGroves.Mobile.Service;
using MomentumGroves.Mobile.DTO;

namespace MomentumGroves.Mobile.Views;

/// <summary>
/// Provides the private food entry screen.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly LocalFoodService _storage;
    private readonly SyncService _sync;
    private readonly OpenFoodFactsService _openFoodFacts;
    private List<FoodDto> _foods = new();
    private Guid? _editingId;
    private bool _initialized;

    /// <summary>
    /// Initializes the food entry screen.
    /// </summary>
    public MainPage(LocalFoodService storage, SyncService sync, OpenFoodFactsService openFoodFacts)
    {
        InitializeComponent();
        _storage = storage;
        _sync = sync;
        _openFoodFacts = openFoodFacts;
        PcAddressInput.Text = _sync.Address;
        UnitInput.SelectedIndex = 0;
    }

    /// <summary>
    /// Loads saved foods when the page appears.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
        if (!_initialized)
        {
            try
            {
                PcSecretInput.Text = await _sync.GetSecretAsync();
            }
            catch (Exception)
            {
                TransferStatus.Text = "Couldn't read the sync key. Enter and save it again.";
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Opens the camera and fills the existing form after a successful lookup.
    /// </summary>
    private async void ScanBarcodeClicked(object sender, EventArgs e)
    {
        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
        {
            await DisplayAlert("Camera access", "Allow camera access to scan barcodes.", "OK");
            return;
        }

        var scanner = new BarcodeScannerPage();
        scanner.BarcodeScanned += async (_, barcode) =>
        {
            await Navigation.PopModalAsync();
            ScanStatus.IsVisible = true;
            ScanStatus.Text = "Looking up the barcode in Open Food Facts…";
            try
            {
                var product = await _openFoodFacts.LookupAsync(barcode);
                if (product is null)
                {
                    ScanStatus.Text = "Product not found. You can enter it manually.";
                    return;
                }

                _editingId = null;
                NameInput.Text = product.Name;
                UnitInput.SelectedIndex = product.Unit == "ml" ? 1 : 0;
                KcalInput.Text = product.Kcal.ToString(CultureInfo.InvariantCulture);
                ProteinInput.Text = product.Protein.ToString(CultureInfo.InvariantCulture);
                CarbsInput.Text = product.Carbs.ToString(CultureInfo.InvariantCulture);
                FatInput.Text = product.Fat.ToString(CultureInfo.InvariantCulture);
                FiberInput.Text = product.Fiber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                ScanStatus.Text = "Check the nutrition values and unit before saving.";
            }
            catch (Exception ex)
            {
                ScanStatus.Text = "Lookup failed. " + ex.Message;
            }
        };
        await Navigation.PushModalAsync(new NavigationPage(scanner));
    }

    /// <summary>
    /// Saves the entered food after validation.
    /// </summary>
    private async void SaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameInput.Text))
        {
            await DisplayAlert("Missing name", "Enter the food name.", "OK");
            return;
        }

        if (!TryNumber(KcalInput.Text, out var kcal) || !TryNumber(ProteinInput.Text, out var protein) ||
            !TryNumber(CarbsInput.Text, out var carbs) || !TryNumber(FatInput.Text, out var fat))
        {
            await DisplayAlert("Invalid values",
                "Enter zero or a positive number for calories, protein, carbs, and fat.", "OK");
            return;
        }

        double? fiber = null;
        if (!string.IsNullOrWhiteSpace(FiberInput.Text))
        {
            if (!TryNumber(FiberInput.Text, out var fiberValue))
            {
                await DisplayAlert("Invalid value", "Enter zero or a positive number for fiber.", "OK");
                return;
            }

            fiber = fiberValue;
        }

        var food = new FoodDto
        {
            Id = _editingId ?? Guid.NewGuid(),
            Name = NameInput.Text.Trim(),
            Unit = UnitInput.SelectedIndex == 1 ? "ml" : "g",
            Kcal = kcal,
            Protein = protein,
            Carbs = carbs,
            Fat = fat,
            Fiber = fiber
        };
        try
        {
            await _storage.SaveAsync(food);
            ClearForm();
            await RefreshAsync();
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Couldn't save the food on this phone.", "OK");
        }
    }

    /// <summary>
    /// Validates a nonnegative numeric field.
    /// </summary>
    /// <returns>True when the field is valid.</returns>
    private static bool TryNumber(string? value, out double number)
    {
        var normalized = (value ?? string.Empty).Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
               double.IsFinite(number) && number >= 0;
    }

    /// <summary>
    /// Clears the current food form.
    /// </summary>
    private void ClearClicked(object sender, EventArgs e)
    {
        ClearForm();
    }

    /// <summary>
    /// Resets input fields.
    /// </summary>
    private void ClearForm()
    {
        _editingId = null;
        NameInput.Text = string.Empty;
        UnitInput.SelectedIndex = 0;
        KcalInput.Text = string.Empty;
        ProteinInput.Text = string.Empty;
        CarbsInput.Text = string.Empty;
        FatInput.Text = string.Empty;
        FiberInput.Text = string.Empty;
    }

    /// <summary>
    /// Updates the food list after a search.
    /// </summary>
    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        RenderFoods();
    }

    /// <summary>
    /// Reloads foods from private local storage.
    /// </summary>
    private async Task RefreshAsync()
    {
        try
        {
            _foods = await _storage.GetFoodsAsync();
            if (_editingId is Guid id && _foods.All(food => food.Id != id))
                ClearForm();

            RenderFoods();
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Couldn't load saved foods.", "OK");
        }
    }

    /// <summary>
    /// Renders saved foods with edit and delete actions.
    /// </summary>
    private void RenderFoods()
    {
        FoodsList.Children.Clear();
        var search = SearchInput.Text?.Trim() ?? string.Empty;
        var matches = _foods.Where(food => food.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(food => food.Name).ToList();
        EmptyLabel.IsVisible = matches.Count == 0;
        foreach (var food in matches)
        {
            var details = new Label
            {
                Text =
                    $"{food.Kcal:0.#} kcal · P {food.Protein:0.#} · C {food.Carbs:0.#} · F {food.Fat:0.#} per 100 {food.Unit}",
                FontSize = 12,
                TextColor = Color.FromArgb("#9ba9ac")
            };
            var edit = new Button
            {
                Text = "Edit", FontSize = 12, BackgroundColor = Color.FromArgb("#34413b"), TextColor = Colors.White
            };
            edit.Clicked += (_, _) => EditFood(food);
            var delete = new Button
            {
                Text = "Delete", FontSize = 12, BackgroundColor = Color.FromArgb("#5a3335"), TextColor = Colors.White
            };
            delete.Clicked += async (_, _) => await DeleteFoodAsync(food);
            var actions = new HorizontalStackLayout { Spacing = 8, Children = { edit, delete } };
            var content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label
                    {
                        Text = food.Name + "  • Pending sync", FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White
                    },
                    details, actions
                }
            };
            FoodsList.Children.Add(new Border
            {
                Stroke = Color.FromArgb("#303a40"), BackgroundColor = Color.FromArgb("#1a2025"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
                Padding = new Thickness(12), Content = content
            });
        }
    }

    /// <summary>
    /// Populates the form for editing.
    /// </summary>
    private void EditFood(FoodDto food)
    {
        _editingId = food.Id;
        NameInput.Text = food.Name;
        UnitInput.SelectedIndex = food.Unit == "ml" ? 1 : 0;
        KcalInput.Text = food.Kcal.ToString(CultureInfo.InvariantCulture);
        ProteinInput.Text = food.Protein.ToString(CultureInfo.InvariantCulture);
        CarbsInput.Text = food.Carbs.ToString(CultureInfo.InvariantCulture);
        FatInput.Text = food.Fat.ToString(CultureInfo.InvariantCulture);
        FiberInput.Text = food.Fiber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        ShowCreate();
    }

    /// <summary>
    /// Deletes a food after confirmation.
    /// </summary>
    private async Task DeleteFoodAsync(FoodDto food)
    {
        if (!await DisplayAlert("Delete food", $"Delete {food.Name}?", "Delete", "Cancel"))
        {
            return;
        }

        try
        {
            await _storage.DeleteAsync(food.Id);
            if (_editingId == food.Id)
            {
                ClearForm();
            }

            await RefreshAsync();
        }
        catch (Exception)
        {
            await DisplayAlert("Error", "Couldn't delete the food.", "OK");
        }
    }

    /// <summary>Displays the food creation tab.</summary>
    private void ShowCreateClicked(object sender, EventArgs e) => ShowCreate();

    /// <summary>Displays the locally stored foods tab.</summary>
    private void ShowListClicked(object sender, EventArgs e)
    {
        CreatePanel.IsVisible = false;
        ListPanel.IsVisible = true;
        CreateTab.BackgroundColor = Color.FromArgb("#27312b");
        CreateTab.TextColor = Colors.White;
        ListTab.BackgroundColor = Color.FromArgb("#a6da7e");
        ListTab.TextColor = Color.FromArgb("#101417");
        RenderFoods();
    }

    private void ShowCreate()
    {
        CreatePanel.IsVisible = true;
        ListPanel.IsVisible = false;
        CreateTab.BackgroundColor = Color.FromArgb("#a6da7e");
        CreateTab.TextColor = Color.FromArgb("#101417");
        ListTab.BackgroundColor = Color.FromArgb("#27312b");
        ListTab.TextColor = Colors.White;
    }

    /// <summary>Saves pairing settings for manual sync.</summary>
    private async void TransferClicked(object sender, EventArgs e)
    {
        var address = PcAddressInput.Text?.Trim() ?? "";
        var secret = PcSecretInput.Text?.Trim() ?? "";
        if (!SyncService.IsHttpsAddress(address) || secret.Length < 16)
        {
            TransferStatus.Text = "Enter your PC's HTTPS address and a sync key of at least 16 characters.";
            return;
        }

        TransferButton.IsEnabled = false;
        try
        {
            await _sync.SaveSecretAsync(secret);
            _sync.Address = address;
            TransferStatus.Text = "Settings saved. To transfer data, tap Sync to PC on the home screen.";
        }
        catch (Exception)
        {
            TransferStatus.Text = "Couldn't save the sync settings.";
        }
        finally
        {
            TransferButton.IsEnabled = true;
        }
    }
}
