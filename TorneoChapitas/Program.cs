using Microsoft.AspNetCore.Authentication.Cookies;
using TorneoAmigos.Data;

var builder = WebApplication.CreateBuilder(args);

// Leer DATABASE_URL de Railway si existe (formato postgresql://user:pass@host:port/db)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    // Convertir formato postgresql:// a formato Npgsql
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};Trust Server Certificate=true;SSL Mode=Require";
    builder.Configuration["ConnectionStrings:TorneoAmigosDB"] = connectionString;
}

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<TorneoRepository>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/Auth/Login";
        options.LogoutPath        = "/Auth/Logout";
        options.AccessDeniedPath  = "/Auth/Login";
        options.ExpireTimeSpan    = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Error interno del servidor.");
    });
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
