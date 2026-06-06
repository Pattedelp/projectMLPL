var builder = WebApplication.CreateBuilder(args);

// Agregar MVC con Razor Views
builder.Services.AddControllersWithViews();

// Registrar el repositorio para inyección de dependencias
builder.Services.AddScoped<TorneoAmigos.Data.TorneoRepository>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();