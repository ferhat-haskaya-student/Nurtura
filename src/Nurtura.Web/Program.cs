using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core;
using Nurtura.Core.Identity;
using Nurtura.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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

    await IdentitySeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        adminEmail,
        adminPassword);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
