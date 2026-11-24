using System.Globalization;
using MomentumGroves.Mobile.Configuration;
using MomentumGroves.Mobile.Data;
using MomentumGroves.Mobile.Service;
using MomentumGroves.Mobile.DTO;

namespace MomentumGroves.Mobile.Views;

/// <summary>
/// Provides the private food entry screen.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly IFoodStore _storage;
    private readonly DataProvider _provider;
    private readonly OpenFoodFactsService _openFoodFacts;
    private List<FoodDto> _foods = new();
    private FoodDto? _editing;
    private Guid _requestId = Guid.NewGuid();
    private bool _saving;

    /// <summary>
    /// Initializes the food entry screen.
    /// </summary>
    public MainPage(IFoodStore storage, OpenFoodFactsService openFoodFacts, DataProvider provider)
    {
        InitializeComponent();
        _storage = storage;
        _provider = provider;
        _openFoodFacts = openFoodFacts;
        UnitInput.SelectedIndex = 0;
    }

    /// <summary>
    /// Loads saved foods when the page appears.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();

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

                _editing = null;
                _requestId = Guid.NewGuid();
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
        if (_saving)
            return;

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
            Id = _editing?.Id ?? _requestId,
            CloudId = _editing?.CloudId,
            CloudVersion = _editing?.CloudVersion,
            Name = NameInput.Text.Trim(),
            Unit = UnitInput.SelectedIndex == 1 ? "ml" : "g",
            Kcal = kcal,
            Protein = protein,
            Carbs = carbs,
            Fat = fat,
            Fiber = fiber
        };
        _saving = true;
        try
        {
            await _storage.SaveAsync(food);
            ClearForm();
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlert("Error", exception is DataStoreException ? exception.Message : "Couldn't save the food.", "OK");
        }
        finally
        {
            _saving = false;
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
        _editing = null;
        _requestId = Guid.NewGuid();
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
            if (_editing is { CloudId: null } editing && _foods.All(food => food.Id != editing.Id))
                ClearForm();

            RenderFoods();
        }
        catch (Exception exception)
        {
            _foods.Clear();
            RenderFoods();
            await DisplayAlert("Error", exception is DataStoreException ? exception.Message : "Couldn't load saved foods.", "OK");
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
        _editing = food;
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
            await _storage.DeleteAsync(food);
            if (_editing?.Id == food.Id)
            {
                ClearForm();
            }

            await RefreshAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlert("Error", exception is DataStoreException ? exception.Message : "Couldn't delete the food.", "OK");
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

    private async void RefreshClicked(object sender, EventArgs e) => await RefreshAsync();

    private void ShowCreate()
    {
        CreatePanel.IsVisible = true;
        ListPanel.IsVisible = false;
        CreateTab.BackgroundColor = Color.FromArgb("#a6da7e");
        CreateTab.TextColor = Color.FromArgb("#101417");
        ListTab.BackgroundColor = Color.FromArgb("#27312b");
        ListTab.TextColor = Colors.White;
    }

}
