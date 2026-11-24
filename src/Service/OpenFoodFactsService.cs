using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MomentumGroves.Mobile.Models;

namespace MomentumGroves.Mobile.Service;

/// <summary>
/// Looks up scanned retail products without persisting their information.
/// </summary>
public sealed class OpenFoodFactsService
{
    private readonly HttpClient _http = new();

    /// <summary>
    /// Looks up a barcode and returns nutrition per 100 grams or millilitres.
    /// </summary>
    public async Task<ScannedFood?> LookupAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (!Regex.IsMatch(barcode, @"^\d{8,14}$"))
        {
            throw new InvalidOperationException("This barcode is not a supported EAN or UPC number.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://world.openfoodfacts.org/api/v2/product/{barcode}.json?fields=status,product_name,product_name_da,quantity,nutriments");
        request.Headers.UserAgent.ParseAdd("MomentumGrovesMobile/1.0 (private food diary)");
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var root = json.RootElement;
        if (!root.TryGetProperty("status", out var status) || status.GetInt32() != 1 ||
            !root.TryGetProperty("product", out var product))
        {
            return null;
        }

        var name = ReadString(product, "product_name_da") ?? ReadString(product, "product_name");
        if (string.IsNullOrWhiteSpace(name) || !product.TryGetProperty("nutriments", out var nutrients))
        {
            throw new InvalidOperationException("Open Food Facts is missing the product name or nutrition information.");
        }

        var kcal = ReadNumber(nutrients, "energy-kcal_100g");
        if (kcal is null && ReadNumber(nutrients, "energy_100g") is { } kj)
        {
            kcal = kj / 4.184;
        }

        var protein = ReadNumber(nutrients, "proteins_100g");
        var carbs = ReadNumber(nutrients, "carbohydrates_100g");
        var fat = ReadNumber(nutrients, "fat_100g");
        if (kcal is null || protein is null || carbs is null || fat is null)
        {
            throw new InvalidOperationException(
                "Some required nutrition values are missing. Enter them manually.");
        }

        var quantity = ReadString(product, "quantity") ?? string.Empty;
        var liquid = Regex.IsMatch(quantity, @"\b(?:\d+[.,]?\d*\s*)?(?:ml|cl|dl|l)\b", RegexOptions.IgnoreCase);
        return new ScannedFood(name, liquid ? "ml" : "g", kcal.Value, protein.Value, carbs.Value, fat.Value,
            ReadNumber(nutrients, "fiber_100g"));
    }

    private static string? ReadString(JsonElement parent, string key)
    {
        return parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static double? ReadNumber(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var value))
        {
            return null;
        }

        double number = 0;
        var valid = value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out number);
        if (!valid && value.ValueKind == JsonValueKind.String)
        {
            valid = double.TryParse(value.GetString()?.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number);
        }

        return valid && double.IsFinite(number) && number >= 0 ? number : null;
    }
}
