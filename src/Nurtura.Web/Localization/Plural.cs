using System.Globalization;

namespace Nurtura.Web.Localization;

// Picks the plural form of a text for a number: the key is baseKey + "_One", "_Few" or "_Many".
// Polish has three forms (1 dzień, 2–4 dni, 5 dni; 12–14 take the "many" form, 22–24 "few" again).
// English has two, so its "_Few" texts are the same as its "_Many" ones.
public static class Plural
{
    public static string Key(string baseKey, int count) => baseKey + "_" + Form(count);

    private static string Form(int count)
    {
        if (count == 1)
        {
            return "One";
        }

        var isPolish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl";
        var lastDigit = count % 10;
        var lastTwoDigits = count % 100;
        return isPolish && lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14 ? "Few" : "Many";
    }
}
