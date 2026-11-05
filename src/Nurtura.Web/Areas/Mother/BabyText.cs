using Microsoft.Extensions.Localization;
using Nurtura.Core.Services;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Areas.Mother;

// How a baby's age reads: on the baby page and in the professional's pre-consultation summary.
public static class BabyText
{
    // "3 months, 2 weeks": the two units BabyAge kept, leaving out a zero second part.
    public static string Age(IStringLocalizer localizer, BabyAge age)
    {
        var parts = new List<string>();
        void Add(string unit, int count, bool always = false)
        {
            if (count > 0 || always)
            {
                parts.Add(localizer[Plural.Key("Baby_Age_" + unit, count), count]);
            }
        }

        if (age.Years > 0)
        {
            Add("Years", age.Years);
            Add("Months", age.Months);
        }
        else if (age.Months > 0)
        {
            Add("Months", age.Months);
            Add("Weeks", age.Weeks);
        }
        else
        {
            Add("Weeks", age.Weeks);
            Add("Days", age.Days, always: age.Weeks == 0);
        }

        return parts.Count == 2 ? localizer["Baby_Age_Join", parts[0], parts[1]] : parts[0];
    }
}
