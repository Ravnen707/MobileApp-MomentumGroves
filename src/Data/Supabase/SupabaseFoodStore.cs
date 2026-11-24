using System.Buffers.Binary;
using MomentumGroves.Mobile.DTO;
using MomentumGroves.Mobile.Service.Supabase;

namespace MomentumGroves.Mobile.Data.Supabase;

internal sealed class SupabaseFoodStore(SupabaseSessionService session) : IFoodStore
{
    public async Task<List<FoodDto>> GetFoodsAsync()
    {
        var foods = new List<FoodDto>();
        long? cursor = null;
        List<CloudFood> page;
        do
        {
            page = await session.RpcAsync<List<CloudFood>>("mobile_foods", new { after_id = cursor });
            foreach (var item in page)
            {
                var id = new byte[16];
                BinaryPrimitives.WriteInt64LittleEndian(id, item.Id);
                foods.Add(new()
                {
                    Id = new Guid(id), CloudId = item.Id, CloudVersion = item.Version,
                    Name = item.Name, Unit = item.Unit, Kcal = item.Kcal, Protein = item.Protein,
                    Carbs = item.Carbs, Fat = item.Fat, Fiber = item.Fiber
                });
                cursor = item.Id;
            }
        } while (page.Count == 100);

        return foods;
    }

    public async Task SaveAsync(FoodDto food)
    {
        var result = await session.RpcAsync<Mutation>("mobile_save_food", new
        {
            food_id = food.CloudId, expected_version = food.CloudVersion, request_id = food.Id,
            food = new { name = food.Name, unit = food.Unit, kcal = food.Kcal, protein = food.Protein,
                carbs = food.Carbs, fat = food.Fat, fiber = food.Fiber }
        });
        Check(result);
    }

    public async Task DeleteAsync(FoodDto food) =>
        Check(await session.RpcAsync<Mutation>("mobile_delete_food",
            new { food_id = food.CloudId, expected_version = food.CloudVersion }));

    private static void Check(Mutation result)
    {
        if (result.Status == "saved")
            return;

        throw new DataStoreException(result.Status switch
        {
            "duplicate" => "A food with this name already exists. Refresh to see it.",
            "conflict" => "This food changed. Refresh and reopen it before trying again.",
            "in_use" => "This food is used in a recipe and cannot be deleted.",
            "invalid" => "Check the food name, unit and nutrition values.",
            _ => "Supabase could not complete the food update."
        });
    }

    private sealed record Mutation(string Status);
    private sealed record CloudFood(long Id, string Version, string Name, string Unit, double Kcal,
        double Protein, double Carbs, double Fat, double? Fiber);
}
