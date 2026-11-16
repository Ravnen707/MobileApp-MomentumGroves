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
        PcSecretInput.Text = _sync.Secret;
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
            await DisplayAlert("Kameraadgang", "Giv appen kameraadgang for at scanne stregkoder.", "OK");
            return;
        }

        var scanner = new BarcodeScannerPage();
        scanner.BarcodeScanned += async (_, barcode) =>
        {
            await Navigation.PopModalAsync();
            ScanStatus.IsVisible = true;
            ScanStatus.Text = "Slår stregkoden op i Open Food Facts...";
            try
            {
                var product = await _openFoodFacts.LookupAsync(barcode);
                if (product is null)
                {
                    ScanStatus.Text = "Produktet blev ikke fundet. Du kan indtaste det manuelt.";
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
                ScanStatus.Text = "Felterne er udfyldt. Kontrollér værdier og enhed før du gemmer.";
            }
            catch (Exception ex)
            {
                ScanStatus.Text = "Opslag mislykkedes: " + ex.Message;
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
            await DisplayAlert("Manglende navn", "Indtast fødevarens navn.", "OK");
            return;
        }

        if (!TryNumber(KcalInput.Text, out var kcal) || !TryNumber(ProteinInput.Text, out var protein) ||
            !TryNumber(CarbsInput.Text, out var carbs) || !TryNumber(FatInput.Text, out var fat))
        {
            await DisplayAlert("Ugyldige tal",
                "Udfyld kalorier, protein, kulhydrat og fedt med tal på 0 eller derover.", "OK");
            return;
        }

        double? fiber = null;
        if (!string.IsNullOrWhiteSpace(FiberInput.Text))
        {
            if (!TryNumber(FiberInput.Text, out var fiberValue))
            {
                await DisplayAlert("Ugyldigt tal", "Kostfibre skal være et tal på 0 eller derover.", "OK");
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
            await DisplayAlert("Fejl", "Fødevaren kunne ikke gemmes på telefonen.", "OK");
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
            RenderFoods();
        }
        catch (Exception)
        {
            await DisplayAlert("Fejl", "Lokale fødevarer kunne ikke indlæses.", "OK");
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
                    $"{food.Kcal:0.#} kcal · P {food.Protein:0.#} · K {food.Carbs:0.#} · F {food.Fat:0.#} pr. 100 {food.Unit}",
                FontSize = 12,
                TextColor = Color.FromArgb("#9ba9ac")
            };
            var edit = new Button
            {
                Text = "Rediger", FontSize = 12, BackgroundColor = Color.FromArgb("#34413b"), TextColor = Colors.White
            };
            edit.Clicked += (_, _) => EditFood(food);
            var delete = new Button
                { Text = "Slet", FontSize = 12, BackgroundColor = Color.FromArgb("#5a3335"), TextColor = Colors.White };
            delete.Clicked += async (_, _) => await DeleteFoodAsync(food);
            var actions = new HorizontalStackLayout { Spacing = 8, Children = { edit, delete } };
            var content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label
                    {
                        Text = food.Name + "  • Afventer", FontAttributes = FontAttributes.Bold,
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
        if (!await DisplayAlert("Slet fødevare", $"Vil du slette {food.Name}?", "Slet", "Annuller"))
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
            await DisplayAlert("Fejl", "Fødevaren kunne ikke slettes.", "OK");
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

    /// <summary>Transfers locally saved foods to the paired desktop.</summary>
    private async void TransferClicked(object sender, EventArgs e)
    {
        _sync.Address = PcAddressInput.Text ?? "";
        _sync.Secret = PcSecretInput.Text ?? "";
        TransferButton.IsEnabled = false;
        TransferStatus.Text = "Forbinder til PC...";
        try
        {
            var result = await _sync.TransferAsync();
            TransferStatus.Text =
                $"Behandlet: {result.sent}. Nye på PC: {result.imported}. Allerede eksisterende: {result.skipped}.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            TransferStatus.Text = "Overførsel mislykkedes: " + ex.Message;
        }
        finally
        {
            TransferButton.IsEnabled = true;
        }
    }
}