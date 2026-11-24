using MomentumGroves.Mobile.Service.Supabase;
using static MomentumGroves.Mobile.DTO.TrainingData;

namespace MomentumGroves.Mobile.Data.Supabase;

internal sealed class SupabaseTrainingStore(SupabaseSessionService session, SupabaseTrainingImageCache? images = null) : ITrainingStore
{
    private sealed record WeightOutcome(string Status, decimal? Kg);

    public async Task<TrainingState> ReadAsync()
    {
        var programmes = await session.RpcAsync<Programmes>("mobile_training", new { });
        return new(programmes, [])
        {
            CloudImages = images is null ? null : await images.RefreshAsync(programmes)
        };
    }

    public async Task SaveWeightAsync(string exerciseId, decimal kg, decimal baseline)
    {
        var outcome = await session.RpcAsync<WeightOutcome>("mobile_save_weight",
            new { exercise_id = exerciseId, expected_kg = baseline, requested_kg = kg });
        if (outcome.Status != "saved")
            throw new DataStoreException(outcome.Status switch
            {
                "conflict" => $"The current shared weight is {outcome.Kg} kg. Refresh and review before saving again.",
                "missing" => "This exercise is unavailable. Refresh the programme.",
                _ => "Enter a valid weight and try again."
            });
    }

    public Task DiscardConflictsAsync() => Task.CompletedTask;
}
