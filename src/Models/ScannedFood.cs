namespace MomentumGroves.Mobile.Models;

/// <summary>
/// Holds a scanned product until the user decides whether to save it.
/// </summary>
public sealed record ScannedFood(
    string Name,
    string Unit,
    double Kcal,
    double Protein,
    double Carbs,
    double Fat,
    double? Fiber);