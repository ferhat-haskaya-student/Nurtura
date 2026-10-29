using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nurtura.Core;
using Nurtura.Core.Identity;
using Nurtura.Data;
using Nurtura.Web;
using Nurtura.Web.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Interface texts come from Resources/en.json and pl.json (see JsonStringLocalizerFactory).
// Registered before AddLocalization so it replaces the default .resx-based factory.
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
builder.Services.AddLocalization();
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

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

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<NurturaDbContext>();

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

    // Test accounts exist only in Development.
    if (app.Environment.IsDevelopment())
    {
        var testPassword = app.Configuration["Seed:TestPassword"]
            ?? throw new InvalidOperationException("Configuration value 'Seed:TestPassword' not found.");

        await IdentitySeeder.SeedTestUsersAsync(userManager, testPassword);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
