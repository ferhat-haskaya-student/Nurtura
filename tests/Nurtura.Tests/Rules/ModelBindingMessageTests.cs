using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nurtura.Core.Entities;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Rules;

// ASP.NET's own messages about a value that does not fit a field come from the language files, in the language of the
// request: from the server, and in the number boxes, where jQuery Validate would otherwise show its own English texts.
public sealed partial class ModelBindingMessageTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private sealed record Texts(string Number, string Invalid, string Missing, string MinutesRange, string MlRange, string WeekRange);

    private static readonly Dictionary<string, Texts> Expected = new()
    {
        ["en"] = new("Enter a number.", "This value is not valid.", "Enter a value.",
            "Enter a number from 1 to 120.", "Enter a number from 1 to 500.", "Enter a number from 22 to 44."),
        ["pl"] = new("Wpisz liczbę.", "Ta wartość jest nieprawidłowa.", "Wpisz wartość.",
            "Wpisz liczbę od 1 do 120.", "Wpisz liczbę od 1 do 500.", "Wpisz liczbę od 22 do 44.")
    };

    // ---- The server

    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    public async Task LettersInMinutes_MessageInTheBrowsersLanguage_NothingSaved(string language)
    {
        using var a = await SignInAsync(Users.A.Email, language);

        var response = await a.PostAsync("/Mother/Feeding/Save", Feed(minutes: "abc"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Expected[language].Invalid, Message(await response.Content.ReadAsStringAsync(), "Minutes"));
        Assert.Equal(0, await Data.DbAsync(db => db.FeedingRecords.CountAsync()));
    }

    [Fact]
    public async Task LettersInMinutes_PolishCookieInAnEnglishBrowser_PolishMessage()
    {
        using var a = await SignInAsync(Users.A.Email, "en");
        await a.PostAsync("/Language/Set", new() { ["culture"] = "pl", ["returnUrl"] = "/" });

        var response = await a.PostAsync("/Mother/Feeding/Save", Feed(minutes: "abc"));

        Assert.Equal(Expected["pl"].Invalid, Message(await response.Content.ReadAsStringAsync(), "Minutes"));
    }

    [Fact]
    public async Task NumberInMinutes_Saved()
    {
        using var a = await SignInAsync(Users.A.Email, "pl");

        AssertRedirect(await a.PostAsync("/Mother/Feeding/Save", Feed(minutes: "15")), "/Mother/Baby");

        Assert.Equal([15], await Data.DbAsync(db => db.FeedingRecords.Select(f => f.Minutes).ToListAsync()));
    }

    // All of ASP.NET's messages, on the same running app in one language after the other.
    [Fact]
    public void EveryMessage_FollowsTheLanguage()
    {
        var messages = Factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value.ModelBindingMessageProvider;
        var before = CultureInfo.CurrentUICulture;

        try
        {
            foreach (var language in new[] { "en", "pl", "en" })
            {
                CultureInfo.CurrentUICulture = new CultureInfo(language);
                var texts = Expected[language];

                Assert.Equal(texts.Number, messages.ValueMustBeANumberAccessor("Minutes"));
                Assert.Equal(texts.Number, messages.NonPropertyValueMustBeANumberAccessor());

                Assert.Equal(texts.Invalid, messages.AttemptedValueIsInvalidAccessor("abc", "Minutes"));
                Assert.Equal(texts.Invalid, messages.NonPropertyAttemptedValueIsInvalidAccessor("abc"));
                Assert.Equal(texts.Invalid, messages.UnknownValueIsInvalidAccessor("Minutes"));
                Assert.Equal(texts.Invalid, messages.NonPropertyUnknownValueIsInvalidAccessor());
                Assert.Equal(texts.Invalid, messages.ValueIsInvalidAccessor("abc"));

                Assert.Equal(texts.Missing, messages.MissingBindRequiredValueAccessor("Minutes"));
                Assert.Equal(texts.Missing, messages.MissingKeyOrValueAccessor());
                Assert.Equal(texts.Missing, messages.MissingRequestBodyRequiredValueAccessor());
                Assert.Equal(texts.Missing, messages.ValueMustNotBeNullAccessor(""));
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    // ---- The number boxes

    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    public async Task NumberBoxes_CarryTheTexts(string language)
    {
        using var a = await SignInAsync(Users.A.Email, language);
        var texts = Expected[language];

        var feeding = await a.GetHtmlAsync($"/Mother/Feeding/Log/{Users.A.BabyId}");
        AssertTexts(feeding, "Minutes", texts.Number, texts.MinutesRange);
        AssertTexts(feeding, "AmountMl", texts.Number, texts.MlRange);
        AssertTexts(await a.GetHtmlAsync("/Mother/Baby/Add"), "BirthWeek", texts.Number, texts.WeekRange);
    }

    // The real feeding page with its real scripts: what jQuery Validate shows under the box.
    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    public async Task MinutesBox_InTheBrowser_ShowsTheTexts(string language)
    {
        using var a = await SignInAsync(Users.A.Email, language);
        var html = await a.GetHtmlAsync($"/Mother/Feeding/Log/{Users.A.BabyId}");
        var texts = Expected[language];

        // EdgePage reads the server's time from the page; this page has none.
        html = html.Replace("<main ", $"<main data-server-now=\"{EdgePage.Milliseconds(DateTime.UtcNow)}\" ");
        var seen = await new EdgePage(Factory, html).RunAsync<string[]>("""
            document.getElementById('type-Breast').click();
            var box = document.querySelector('[name=Minutes]');
            var message = document.querySelector('[data-valmsg-for=Minutes]');
            var seen = [];
            function check() { $(box).valid(); seen.push(message.textContent); }

            // Edge lets only e, + and - into a number box, as a person types.
            box.focus();
            document.execCommand('insertText', false, 'e');
            if (!box.validity.badInput) { throw new Error('No letter in the box.'); }
            check();
            ['0', '121', '2.5', '15'].forEach(function (value) { box.value = value; check(); });
            return seen;
            """);

        Assert.Equal([texts.Number, texts.MinutesRange, texts.MinutesRange, texts.Number, ""], seen);
    }

    // ---- Helpers

    private async Task<AppClient> SignInAsync(string email, string language)
    {
        var client = await SignInAsync(email);
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        return client;
    }

    private Dictionary<string, string> Feed(string minutes)
    {
        var time = DateTime.UtcNow.AddMinutes(-30);
        return new()
        {
            ["babyId"] = Users.A.BabyId.ToString(),
            ["feedId"] = "",
            ["Date"] = SleepForm.DateText(time),
            ["Time"] = SleepForm.TimeText(time),
            ["Type"] = nameof(FeedingType.Breast),
            ["BreastSide"] = nameof(BreastSide.Left),
            ["Minutes"] = minutes,
            ["IsShared"] = "false"
        };
    }

    private static string Message(string html, string field) =>
        WebUtility.HtmlDecode(Regex.Match(html, $"<span [^>]*data-valmsg-for=\"{field}\"[^>]*>([^<]*)</span>").Groups[1].Value);

    private static void AssertTexts(string html, string field, string number, string range)
    {
        var box = Assert.Single(InputPattern().Matches(html), m => Attribute(m.Value, "name") == field).Value;
        Assert.Equal(number, Attribute(box, "data-val-number"));
        Assert.Equal(number, Attribute(box, "data-msg-step"));
        Assert.Equal(range, Attribute(box, "data-msg-min"));
        Assert.Equal(range, Attribute(box, "data-msg-max"));
        Assert.Equal(range, Attribute(box, "data-val-range"));
    }

    private static string Attribute(string tag, string name) =>
        WebUtility.HtmlDecode(Regex.Match(tag, $" {name}=\"([^\"]*)\"").Groups[1].Value);

    [GeneratedRegex("<input [^>]*>")]
    private static partial Regex InputPattern();
}
