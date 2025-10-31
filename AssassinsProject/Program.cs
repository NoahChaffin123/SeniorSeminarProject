using AssassinsProject.Data;
using AssassinsProject.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages
// In Development, also enable runtime compilation so .cshtml edits are hot-reloaded without rebuilds.
if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddRazorPages()
        .AddRazorRuntimeCompilation(); // optional, requires package below
}
else
{
    builder.Services.AddRazorPages();
}

// EF Core DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// App services
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<FileStorageService>();

var app = builder.Build();

// Static files
app.UseStaticFiles();

// Error pages
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// HTTP pipeline
app.UseHttpsRedirection();
app.UseRouting();

app.MapRazorPages();

app.Run();
