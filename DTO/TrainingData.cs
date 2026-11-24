using System.Text.Json;
using System.Text.Json.Serialization;

namespace MomentumGroves.Mobile.DTO;

public static class TrainingData
{
    public sealed record TrainingGroup(string Id, string Name, List<Programme> Programmes);
    public sealed record Programme(string Id, string Title, string Subtitle, List<Exercise> Exercises);
    public sealed record Exercise(string Id, string Name, string? Description, string? Type,
        decimal Sets, string? Reps, decimal Kg, decimal Minutes,
        [property: JsonConverter(typeof(OptionalImageReferenceConverter))] string? Image);

    public sealed class OptionalImageReferenceConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString();

            // Older or malformed image metadata must not hide the exercise or its weight.
            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
    public sealed record Programmes(List<TrainingGroup> Groups);
    public sealed record WeightEdit(Guid ChangeId, string ExerciseId, decimal BaseKg, decimal Kg,
        string? Status = null);
    public sealed record TrainingState(Programmes? Programmes, List<WeightEdit> Pending, string? Version = null)
    {
        [JsonIgnore]
        public IReadOnlyDictionary<string, string>? CloudImages { get; init; }
    }
}
