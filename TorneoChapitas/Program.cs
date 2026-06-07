using Microsoft.AspNetCore.Authentication.Cookies;
using TorneoAmigos.Data;

var builder = WebApplication.CreateBuilder(args);

// Railway puede pasar la BD de varias formas, las leemos todas
var pgHost     = Environment.GetEnvironmentVariable("PGHOST");
var pgPort     = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
var pgDb       = Environment.GetEnvironmentVariable("PGDATABASE") ?? "railway";
var pgUser     = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD");

if (!string.IsNullOrEmpty(pgHost) && !string.IsNullOrEmpty(pgPassword))
{
    var cs = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPassword};Trust Server Certificate=true;SSL Mode=Require";
    builder.Configuration["ConnectionStrings:TorneoAmigosDB"] = cs;
}
else
{
    // Intentar leer DATABASE_URL como fallback
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        var uri      = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        var cs       = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};Trust Server Certificate=true;SSL Mode=Require";
        builder.Configuration["ConnectionStrings:TorneoAmigosDB"] = cs;
    }
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
