using System;
using AssassinsProject.Data;
using AssassinsProject.Services;
using AssassinsProject.Services.Email;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database ----------
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var conn = builder.Configuration.GetConnectionString("Default")
               ?? throw new InvalidOperationException("Missing connection string 'Default'.");
    opt.UseSqlServer(conn, o => o.EnableRetryOnFailure());
});

// ---------- Razor Pages ----------
builder.Services.AddRazorPages();

// ---------- App Services ----------
builder.Services.AddScoped<GameService>();            
builder.Services.AddSingleton<FileStorageService>();  

// Email sender via IConfiguration (uses Azure.Communication.Email)
builder.Services.AddSingleton<IEmailSender, AzureEmailSender>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();

app.Run();
