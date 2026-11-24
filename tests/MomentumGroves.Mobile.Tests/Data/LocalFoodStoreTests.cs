using MomentumGroves.Mobile.Data;
using MomentumGroves.Mobile.Data.Local;

namespace MomentumGroves.Mobile.Tests.Data;

public sealed class LocalFoodStoreTests
{
    [Test]
    public async Task ExistingStandaloneFoodsRemainReadableAfterAnEdit()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var id = Guid.NewGuid();
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "foods.json"),
                $$"""[{"Id":"{{id}}","Name":"Oats","Unit":"g","Kcal":370},{"Name":"Milk","Unit":"ml"}]""");
            IFoodStore store = new LocalFoodStore(directory.FullName);
            var food = (await store.GetFoodsAsync()).Single(item => item.Id == id);
            food.Kcal = 375;
            await store.SaveAsync(food);

            var reloaded = await new LocalFoodStore(directory.FullName).GetFoodsAsync();
            Assert.Multiple(() =>
            {
                Assert.That(reloaded, Has.Count.EqualTo(2));
                Assert.That(reloaded.Single(item => item.Id == id).Kcal, Is.EqualTo(375));
                Assert.That(reloaded.Any(item => item.Name == "Milk"), Is.True);
            });
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
