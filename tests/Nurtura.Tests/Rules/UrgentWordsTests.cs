using Nurtura.Core.Services;

namespace Nurtura.Tests.Rules;

// The Assistant's urgent words (English and Polish): missing an urgent search is worse than one extra card,
// but ordinary worries must not show it.
public sealed class UrgentWordsTests
{
    [Theory]
    [InlineData("I have heavy bleeding")]
    [InlineData("heavy and sudden bleeding since this morning")]
    [InlineData("My baby has BLUE LIPS")]
    [InlineData("I can't breathe")]
    [InlineData("I want to end my life")]
    [InlineData("silne krwawienie od rana")]
    [InlineData("Nie mogę oddychać")]
    [InlineData("nie moge oddychac")]
    [InlineData("Dziecko jest nieprzytomne")]
    [InlineData("Mam myśli samobójcze")]
    [InlineData("BÓL W KLATCE piersiowej")]
    public void IsUrgent_EnglishAndPolish_True(string search)
    {
        Assert.True(AssistantText.IsUrgent(search));
    }

    [Theory]
    [InlineData("My baby sleeps well at night")]
    [InlineData("How often should I feed my baby?")]
    [InlineData("Is light bleeding normal?")]
    [InlineData("pain in my chest")]
    [InlineData("Ból głowy po porodzie")]
    [InlineData("Nie chcę spać")]
    [InlineData("To jest dla dziecka")]
    public void IsUrgent_OrdinaryText_False(string search)
    {
        Assert.False(AssistantText.IsUrgent(search));
    }
}
