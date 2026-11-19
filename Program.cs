using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using ProjetoPOO.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' não configurada."
    );

var scriptPath = Path.Combine(builder.Environment.ContentRootPath, "Database", "script.sql");

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

var app = builder.Build();

var dbInitializer = new DbInitializer(connectionString, scriptPath);
dbInitializer.Initialize();

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

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
