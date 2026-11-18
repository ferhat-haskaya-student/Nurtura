using System.Globalization;
using Nurtura.Web.Localization;

namespace Nurtura.Tests.Rules;

// Which plural text a number picks (pure rule, no database). Polish: 1 dzień, 2–4 dni, 5 dni; 12–14 take the
// "many" form and 22–24 "few" again. English: only one and many.
public sealed class PluralTests
{
    private static string KeyIn(string culture, int count)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return Plural.Key("Baby_Age_Days", count);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Theory]
    [InlineData(0, "Many")]
    [InlineData(1, "One")]
    [InlineData(2, "Few")]
    [InlineData(4, "Few")]
    [InlineData(5, "Many")]
    [InlineData(11, "Many")]
    [InlineData(12, "Many")]
    [InlineData(14, "Many")]
    [InlineData(21, "Many")]
    [InlineData(22, "Few")]
    [InlineData(24, "Few")]
    [InlineData(25, "Many")]
    [InlineData(112, "Many")]
    [InlineData(122, "Few")]
    public void Key_Polish(int count, string form)
    {
        Assert.Equal("Baby_Age_Days_" + form, KeyIn("pl-PL", count));
    }

    [Theory]
    [InlineData(0, "Many")]
    [InlineData(1, "One")]
    [InlineData(2, "Many")]
    [InlineData(22, "Many")]
    public void Key_English(int count, string form)
    {
        Assert.Equal("Baby_Age_Days_" + form, KeyIn("en-US", count));
    }
}
