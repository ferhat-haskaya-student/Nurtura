using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Content;
using Nurtura.Core.Demo;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Data;
using Nurtura.Data.Assistant;
using Nurtura.Data.Forum;
using Nurtura.Web;
using Nurtura.Web.Areas.Mother;
using Nurtura.Web.Areas.Professional;
using Nurtura.Web.Hubs;
using Nurtura.Web.Identity;
using Nurtura.Web.Localization;
using Nurtura.Web.ModelBinding;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Interface texts come from Resources/en.json and pl.json (see JsonStringLocalizerFactory).
// Registered before AddLocalization so it replaces the default .resx-based factory.
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
builder.Services.AddLocalization();
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<MotherOnboardingFilter>();
        options.Filters.Add<ProfessionalApprovalFilter>();
        options.ModelBinderProviders.Insert(0, new DateOnlyModelBinderProvider());
        options.ModelBinderProviders.Insert(0, new TimeOnlyModelBinderProvider());
    })
    // The API controller checks its ModelState itself: another mother's id must be 404 first, and a refusal is 422 with
    // the form's messages, not the automatic 400.
    .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true)
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
builder.Services.ConfigureOptions<ModelBindingMessages>();

// Language comes from the cookie first, then the browser, then English.
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(SupportedCultures.Default)
        .AddSupportedCultures(SupportedCultures.All)
        .AddSupportedUICultures(SupportedCultures.All);
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

var connectionString = builder.Configuration.GetConnectionString("Nurtura")
    ?? throw new InvalidOperationException("Connection string 'Nurtura' not found.");
builder.Services.AddDbContext<NurturaDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<INurturaDbContext>(sp => sp.GetRequiredService<NurturaDbContext>());

// Password rule: at least 6 characters, nothing else required.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<NurturaDbContext>()
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

// "Continue with Google" for mothers. Optional: without the key in configuration the app still runs
// and the button is not shown.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.ClaimActions.MapJsonKey(GoogleClaimTypes.EmailVerified, "email_verified");

        // She cancelled or Google returned an error: back to the login page with a message, not an error page.
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect(context.Request.PathBase + "/Account/ExternalLoginCallback?remoteError=true");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddScoped<MotherRegistrationService>();
builder.Services.AddScoped<MotherOnboardingService>();
builder.Services.AddScoped<ProfessionalRegistrationService>();
builder.Services.AddScoped<ProfessionalApprovalService>();
builder.Services.AddScoped<ProfessionalAdminService>();
builder.Services.AddScoped<MotherExternalLoginService>();
builder.Services.AddScoped<DailyCheckInService>();
builder.Services.AddScoped<MotherProfileService>();
builder.Services.AddScoped<BabyService>();
builder.Services.AddScoped<SleepService>();
builder.Services.AddScoped<SleepTimerScreen>();
builder.Services.AddScoped<FeedingService>();
builder.Services.AddScoped<BabyRoutineService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ConsultationSummaryService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ConsultationService>();
builder.Services.AddScoped<GuidanceService>();
builder.Services.AddScoped<IKeywordMatcher, PostgresKeywordMatcher>();
builder.Services.AddScoped<AssistantService>();
builder.Services.AddScoped<IForumSearch, PostgresForumSearch>();
builder.Services.AddScoped<ForumService>();
builder.Services.AddScoped<ForumModerationService>();
builder.Services.AddScoped<GuidanceAdminService>();
builder.Services.AddScoped<AdminNumbersService>();
builder.Services.AddScoped<AccountDeletionService>();

// The consultation chat. Presence (who has the chat open) is kept in memory for the whole app.
builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatPresence>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var adminEmail = app.Configuration["Seed:AdminEmail"]
        ?? throw new InvalidOperationException("Configuration value 'Seed:AdminEmail' not found.");
    var adminPassword = app.Configuration["Seed:AdminPassword"]
        ?? throw new InvalidOperationException("Configuration value 'Seed:AdminPassword' not found.");

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    await IdentitySeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        userManager,
        adminEmail,
        adminPassword);

    // The approved guidance pages; adds only pages whose slug is missing, so admin edits are never overwritten.
    var newPageSlugs = await GuidanceSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<INurturaDbContext>());

    // The Nurtura Assistant keywords, only for the pages just added; admin keyword edits are never undone.
    await AssistantKeywordSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<INurturaDbContext>(), newPageSlugs);

    // Test accounts exist only in Development.
    if (app.Environment.IsDevelopment())
    {
        var testPassword = app.Configuration["Seed:TestPassword"]
            ?? throw new InvalidOperationException("Configuration value 'Seed:TestPassword' not found.");

        await IdentitySeeder.SeedTestUsersAsync(
            userManager,
            scope.ServiceProvider.GetRequiredService<INurturaDbContext>(),
            testPassword);
    }
}

// Demo accounts with 3 months of logs, only when F asks for it: dotnet run --project src/Nurtura.Web -- --seed-demo
// (adds them if there are none) or -- --reset-demo (replaces them). Only in Development, never on the test database.
// Seeds and stops; the app is not started.
var resetDemo = args.Contains(DemoSeeder.ResetFlag);
if (resetDemo || args.Contains(DemoSeeder.Flag))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NurturaDbContext>();
    var refusal = DemoSeeder.RefusalReason(app.Environment.IsDevelopment(), db.Database.GetDbConnection().Database);
    if (refusal is not null)
    {
        Console.Error.WriteLine(refusal);
        Environment.ExitCode = 1;
        return;
    }

    var demoPassword = app.Configuration["Seed:TestPassword"]
        ?? throw new InvalidOperationException("Configuration value 'Seed:TestPassword' not found.");
    var demoUsers = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var result = resetDemo
        ? await DemoSeeder.ResetAsync(demoUsers, db, demoPassword, DateTime.UtcNow)
        : await DemoSeeder.SeedAsync(demoUsers, db, demoPassword, DateTime.UtcNow);
    foreach (var line in result.Lines)
    {
        Console.WriteLine(line);
    }

    // A refused reset changed nothing and says why.
    if (resetDemo && !result.Seeded)
    {
        Environment.ExitCode = 1;
    }

    return;
}

// Configure the HTTP request pipeline.

// Security headers on every response, set just before it is sent so the error handler (which clears headers)
// cannot drop them.
// Content-Security-Policy: the browser runs only Nurtura's own script files, so a script hidden in a post, note or
// message never runs. Inline scripts do not run either: they belong in wwwroot/js. Inline styles stay allowed, and
// Bootstrap's icons are data: images. No form-action: it would block the redirect to Google sign-in.
const string contentSecurityPolicy =
    "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
    "connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        // No other site may show Nurtura inside a frame (clickjacking); frame-ancestors says the same.
        headers["X-Frame-Options"] = "DENY";
        headers["Content-Security-Policy"] = contentSecurityPolicy;
        // The browser uses the declared file type and never guesses another.
        headers["X-Content-Type-Options"] = "nosniff";
        // Other sites see only "nurtura", never the full address with record ids.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        // The app never uses the camera, microphone or location.
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        return Task.CompletedTask;
    });
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// An empty error response (a page that does not exist, or another user's record) shows a friendly page in the
// app's layout instead of a blank browser page. The status code stays the same. Not for the chat hub and the API,
// which are called by scripts.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/hubs") && !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}"));

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Checks the token of controllers marked [RequireAntiforgeryToken] (the API). Forms keep [ValidateAntiForgeryToken].
app.UseAntiforgery();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<ChatHub>("/hubs/chat");

app.Run();
