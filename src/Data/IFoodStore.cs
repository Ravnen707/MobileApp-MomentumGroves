using MomentumGroves.Mobile.DTO;

namespace MomentumGroves.Mobile.Data;

public interface IFoodStore
{
    Task<List<FoodDto>> GetFoodsAsync();
    Task SaveAsync(FoodDto food);
    Task DeleteAsync(FoodDto food);
}
