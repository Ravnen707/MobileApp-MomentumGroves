using System.Text.Json;
using MomentumGroves.Mobile.DTO;

namespace MomentumGroves.Mobile.Data.Local;

/// <summary>
/// Stores foods in the Android application's private data directory.
/// </summary>
public sealed class LocalFoodStore : IFoodStore
{
    private readonly string _path;

    public LocalFoodStore() : this(FileSystem.AppDataDirectory)
    {
    }

    internal LocalFoodStore(string directory) => _path = Path.Combine(directory, "foods.json");

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Returns locally stored foods.
    /// </summary>
    /// <returns>The saved foods.</returns>
    public async Task<List<FoodDto>> GetFoodsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Saves a food locally.
    /// </summary>
    public async Task SaveAsync(FoodDto food)
    {
        await _gate.WaitAsync();
        try
        {
            var foods = await ReadAsync();
            var index = foods.FindIndex(item => item.Id == food.Id);
            if (index >= 0)
            {
                foods[index] = food;
            }
            else
            {
                foods.Add(food);
            }

            await WriteAsync(foods);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Deletes a food from local storage.
    /// </summary>
    public Task DeleteAsync(FoodDto food) => DeleteAsync(food.Id);

    public async Task DeleteAsync(Guid id)
    {
        await DeleteBatchAsync(new[] {
         id });
    }

    public async Task DeleteBatchAsync(IEnumerable<Guid> ids)
    {
        var acknowledgedIds = ids.ToHashSet();
        await _gate.WaitAsync();
        try
        {
            var foods = await ReadAsync();
            foods.RemoveAll(item => acknowledgedIds.Contains(item.Id));
            await WriteAsync(foods);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reads the private JSON database.
    /// </summary>
    /// <returns>The saved foods.</returns>
    private async Task<List<FoodDto>> ReadAsync()
    {
        if (!File.Exists(_path))
        {
            return new List<FoodDto>();
        }

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<FoodDto>>(stream) ?? new List<FoodDto>();
    }

    /// <summary>
    /// Writes the private JSON database atomically.
    /// </summary>
    private async Task WriteAsync(List<FoodDto> foods)
    {
        var temporaryPath = _path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, foods);
        }

        File.Move(temporaryPath, _path, true);
    }
}
