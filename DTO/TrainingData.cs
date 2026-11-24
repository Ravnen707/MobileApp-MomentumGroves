namespace MomentumGroves.Mobile.DTO;

public static class TrainingData
{
    public sealed record TrainingGroup(string Id, string Name, List<Programme> Programmes);
    public sealed record Programme(string Id, string Title, string Subtitle, List<Exercise> Exercises);
    public sealed record Exercise(string Id, string Name, string? Description, string? Type,
        decimal Sets, string? Reps, decimal Kg, decimal Minutes, string? Image);
    public sealed record Programmes(List<TrainingGroup> Groups);
    public sealed record WeightEdit(Guid ChangeId, string ExerciseId, decimal BaseKg, decimal Kg,
        string? Status = null);
    public sealed record TrainingState(Programmes? Programmes, List<WeightEdit> Pending, string? Version = null)
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public IReadOnlyDictionary<string, string>? CloudImages { get; init; }
    }
}
