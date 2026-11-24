using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Nurtura.Tests.Infrastructure;

// Runs a page of the app, with its own scripts, in the Microsoft Edge that is part of Windows: headless, on a phone-width
// screen, with a new empty profile. Nothing is installed and there is no server: the page's HTML is a temporary file and
// its scripts come from wwwroot.
// Time stands still in the page: the phone's clock moves only when the test's script calls advance(ms).
public sealed partial class EdgePage(NurturaAppFactory factory, string html)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    // The server's time on the page (data-server-now). Set to 37 seconds into the minute the page was made in, so a clock
    // that counts from the minute is clearly different from one that counts from a moment.
    public DateTime ServerNow { get; init; } =
        TestData.Minute(DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(ServerNowPattern().Match(html).Groups[1].Value)).UtcDateTime)
            .AddSeconds(37);

    // How far the phone's own clock is from the server's.
    public TimeSpan PhoneClockOff { get; init; }

    // The phone is set to Tokyo time instead of the PC's time zone.
    public bool PhoneInTokyo { get; init; }

    public static long Milliseconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeMilliseconds();

    // Runs the body of an async function in the loaded page and returns what it returns. It can use pick(), clear(),
    // clock(), advance(), sent and answer from Helpers.
    public async Task<T> RunAsync<T>(string script)
    {
        var folder = Directory.CreateTempSubdirectory("nurtura-edge-");
        try
        {
            var file = Path.Combine(folder.FullName, "page.html");
            await File.WriteAllTextAsync(file, Page(script), new UTF8Encoding(false));
            var dom = await DumpAsync(file, Path.Combine(folder.FullName, "profile"));

            var result = ResultPattern().Match(dom);
            Assert.True(result.Success, "The test's script did not finish in Edge.");
            using var json = JsonDocument.Parse(result.Groups[1].Value);
            Assert.True(json.RootElement.TryGetProperty("value", out var value), json.RootElement.ToString());
            return value.Deserialize<T>(JsonSerializerOptions.Web)!;
        }
        finally
        {
            await DeleteAsync(folder);
        }
    }

    private string Page(string script)
    {
        var webRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        var page = ServerNowPattern().Replace(html, $"data-server-now=\"{Milliseconds(ServerNow)}\"", 1);
        page = AssetPattern().Replace(page, match => $"{match.Groups[1].Value}=\"{new Uri(webRoot).AbsoluteUri.TrimEnd('/')}/");

        var helpers = Helpers.Replace("PHONE_NOW", Milliseconds(ServerNow + PhoneClockOff).ToString());
        if (PhoneInTokyo)
        {
            helpers = Tokyo + helpers;
        }

        page = page.Replace("<head>", $"<head><script>{helpers}</script>");
        return page.Replace("</body>", $"<script>run(async function () {{ {script} }});</script></body>");
    }

    private async Task<string> DumpAsync(string file, string profile)
    {
        var start = new ProcessStartInfo(EdgePath())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--disable-extensions",
                     "--disable-sync", "--disable-background-networking", "--disable-component-update",
                     "--user-data-dir=" + profile, "--window-size=390,844", "--virtual-time-budget=120000", "--dump-dom",
                     new Uri(file).AbsoluteUri
                 })
        {
            start.ArgumentList.Add(argument);
        }

        // Set for programs started from some shells (Git Bash); Edge then starts itself again and its output is lost.
        start.Environment.Remove("__COMPAT_LAYER");

        using var edge = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            var output = edge.StandardOutput.ReadToEndAsync(timeout.Token);
            _ = edge.StandardError.ReadToEndAsync(timeout.Token);
            await edge.WaitForExitAsync(timeout.Token);
            return await output;
        }
        catch (OperationCanceledException)
        {
            edge.Kill(entireProcessTree: true);
            throw new TimeoutException("Edge did not finish.");
        }
    }

    private static string EdgePath()
    {
        var path = new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }
            .Select(folder => Path.Combine(Environment.GetFolderPath(folder), "Microsoft", "Edge", "Application", "msedge.exe"))
            .FirstOrDefault(File.Exists);
        Assert.True(path is not null, "These tests need Microsoft Edge (part of Windows). It was not found.");
        return path;
    }

    // Edge can hold its profile for a moment after it has ended.
    private static async Task DeleteAsync(DirectoryInfo folder)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                folder.Delete(recursive: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 40)
            {
                await Task.Delay(250);
            }
        }
    }

    [GeneratedRegex("data-server-now=\"(\\d+)\"")]
    private static partial Regex ServerNowPattern();

    [GeneratedRegex("\\b(src|href)=\"/(?=js/|lib/|css/)")]
    private static partial Regex AssetPattern();

    [GeneratedRegex("<script id=\"edge-result\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex ResultPattern();

    // Edge has no switch for this: dates and Intl behave as on a phone in Tokyo (UTC+9).
    private const string Tokyo = """
        (function () {
            var RealDate = Date;
            var RealFormat = Intl.DateTimeFormat;
            var SHIFT = 9 * 3600000;
            window.Date = class extends RealDate {
                constructor() {
                    if (arguments.length > 1) {
                        super(RealDate.UTC.apply(null, arguments) - SHIFT);
                    } else {
                        super(arguments.length === 0 ? window.Date.now() : arguments[0]);
                    }
                }
                getTimezoneOffset() { return -540; }
            };
            ['FullYear', 'Month', 'Date', 'Day', 'Hours', 'Minutes', 'Seconds'].forEach(function (name) {
                window.Date.prototype['get' + name] = function () {
                    return new RealDate(this.getTime() + SHIFT)['getUTC' + name]();
                };
            });
            Intl.DateTimeFormat = function (locales, options) {
                return new RealFormat(locales, Object.assign({ timeZone: 'Asia/Tokyo' }, options));
            };
        })();
        """;

    private const string Helpers = """
        var phoneNow = PHONE_NOW;
        Date.now = function () { return phoneNow; };

        // What the page sends: form posts (kept on the page) and calls. answer: what the next call gets.
        var sent = [];
        var answer = null;
        document.addEventListener('submit', function (event) {
            if (!event.defaultPrevented) { sent.push('form'); event.preventDefault(); }
        });
        window.fetch = function (url) {
            sent.push(url);
            return Promise.resolve(new Response(JSON.stringify(answer.body), {
                status: answer.status, headers: { 'Content-Type': 'application/json' }
            }));
        };
        // A file cannot take another address.
        history.replaceState = function () { };

        var errors = [];
        window.addEventListener('error', function (event) { errors.push(event.message); });
        window.addEventListener('unhandledrejection', function (event) { errors.push(String(event.reason)); });

        function wait(ms) { return new Promise(function (done) { setTimeout(done, ms); }); }

        function clock() { return document.querySelector('[data-sleep-clock]').textContent; }

        // The time passes; whatever ticks has ticked.
        async function advance(ms) { phoneNow += ms; await wait(1500); }

        async function open(id) {
            document.querySelector('#' + id + ' [data-time-field-button]').click();
            await wait(1000);
            if (!document.querySelector('.offcanvas.show, .modal.show')) { throw new Error('The picker did not open.'); }
        }

        async function press(selector) {
            document.querySelector(selector).click();
            await wait(1000);
            if (document.querySelector('.offcanvas.show, .modal.show')) { throw new Error('The picker did not close.'); }
        }

        function turn(wheel, value) {
            var option = document.querySelector('[data-wheel="' + wheel + '"] [data-value="' + value + '"]');
            if (!option) { throw new Error('No ' + wheel + ' ' + value + '.'); }
            option.click();
        }

        // Picks "dd-mm-yyyy HH:mm" in a field with the wheels.
        async function pick(id, value) {
            var m = /^(\d\d)-(\d\d)-(\d{4}) (\d\d):(\d\d)$/.exec(value);
            await open(id);
            turn('day', Date.UTC(m[3], m[2] - 1, m[1]));
            turn('hour', Number(m[4]));
            turn('minute', Number(m[5]));
            await press('[data-picker-save]');
        }

        async function clear(id) {
            await open(id);
            await press('[data-picker-clear]');
        }

        function run(test) {
            window.addEventListener('load', async function () {
                var result;
                try {
                    await wait(200);
                    var value = await test();
                    result = errors.length === 0 ? { value: value } : { errors: errors };
                } catch (error) {
                    result = { errors: errors.concat(String(error)) };
                }
                var element = document.createElement('script');
                element.id = 'edge-result';
                element.type = 'application/json';
                element.textContent = JSON.stringify(result).replace(/</g, '\\u003c');
                document.body.appendChild(element);
            });
        }
        """;
}
