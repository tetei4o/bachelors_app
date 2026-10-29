using StoryCups.Web.Content;

namespace StoryCups.IntegrationTests;

/// <summary>Данните за менюто не изискват база; тестовете са тук, защото проектът реферира StoryCups.Web.</summary>
public sealed class CafeMenuTests
{
    [Fact]
    public void Drinks_MatchMenuBoard()
    {
        Assert.Equal(12, CafeMenu.Drinks.Count);
        Assert.Equal(CafeMenu.Drinks.Count, CafeMenu.Drinks.Select(d => d.Name).Distinct().Count());
        Assert.All(CafeMenu.Drinks, d =>
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
