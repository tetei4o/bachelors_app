using StoryCups.Infrastructure.Seeding;
using StoryCups.Web.Content;

namespace StoryCups.IntegrationTests;

/// <summary>Данните за менюто, с които се зарежда таблицата Products, и записът на цените. Не изискват база.</summary>
public sealed class CafeMenuTests
{
    [Fact]
    public void Drinks_MatchMenuBoard()
    {
        Assert.Equal(12, MenuSeed.Drinks.Count);
        Assert.Equal(MenuSeed.Drinks.Count, MenuSeed.Drinks.Select(d => d.Name).Distinct().Count());
        Assert.Equal(MenuSeed.Drinks.Count, MenuSeed.Drinks.Select(d => d.Id).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 12), MenuSeed.Drinks.Select(d => d.SortOrder));
        Assert.All(MenuSeed.Drinks, d =>
        {
            Assert.InRange(d.PriceEur, 0.01m, 20m);
            Assert.InRange(d.VolumeMl, 1, 1000);
        });
    }

    [Theory]
    [InlineData("1.5", "1,50 €")]
    [InlineData("4", "4,00 €")]
    [InlineData("0.7", "0,70 €")]
    public void FormatPrice_UsesBulgarianNotation(string eur, string expected)
    {
        Assert.Equal(expected, CafeMenu.FormatPrice(decimal.Parse(eur, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
