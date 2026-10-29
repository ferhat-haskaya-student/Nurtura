using Microsoft.EntityFrameworkCore;
using Nurtura.Core;
using Nurtura.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("Nurtura")
    ?? throw new InvalidOperationException("Connection string 'Nurtura' not found.");
builder.Services.AddDbContext<NurturaDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<INurturaDbContext>(sp => sp.GetRequiredService<NurturaDbContext>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
