namespace MomentumGroves.Mobile.Configuration;

public sealed record DataProvider(bool IsCloud)
{
    public string Name => IsCloud ? "Supabase" : "Standalone (local JSON)";
    public string FoodSuffix => IsCloud ? "" : "  • Pending sync";
    public string TrainingSaved => IsCloud
        ? "Weight saved to Supabase."
        : "Weight saved on this phone. Open Sync to PC from the home screen to send it.";
}
