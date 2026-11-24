using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.Sqlite;
using ProjetoPOO.Database;
using ProjetoPOO.Repositories.Implementation;
using ProjetoPOO.Repositories.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' não configurada."
    );

var scriptPath = Path.Combine(builder.Environment.ContentRootPath, "Database", "Script.sql");

builder.Services.AddTransient<Func<IDbConnection>>(serviceProvider =>
{
    return () =>
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        connection.Execute("PRAGMA foreign_keys = ON;");
        return connection;
    };
});

builder.Services.AddScoped<IProdutoRepository, ProdutoRepository>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IMovimentacaoRepository, MovimentacaoRepository>();
builder.Services.AddScoped<IVendaRepository, VendaRepository>();

var app = builder.Build();

var dbInitializer = new DbInitializer(connectionString, scriptPath);
dbInitializer.Initialize();

var supportedCultures = new[] { new CultureInfo("pt-BR") };
app.UseRequestLocalization(
    new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture("pt-BR"),
        SupportedCultures = supportedCultures,
        SupportedUICultures = supportedCultures,
    }
);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Produtos}/{action=Index}/{id?}");

app.Run();