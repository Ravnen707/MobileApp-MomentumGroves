using static MomentumGroves.Mobile.DTO.TrainingData;

namespace MomentumGroves.Mobile.Data;

public interface ITrainingStore
{
    Task<TrainingState> ReadAsync();
    Task SaveWeightAsync(string exerciseId, decimal kg, decimal baseline);
    Task DiscardConflictsAsync();
}
