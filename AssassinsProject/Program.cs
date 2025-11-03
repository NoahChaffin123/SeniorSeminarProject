using AssassinsProject.Data;
using AssassinsProject.Services;
using AssassinsProject.Services.Email;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database connection
var dbConn =
    builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["ConnectionStrings:Default"];

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(dbConn));

// Options: Email
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));

// DI registrations
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<IEmailSender, AzureEmailSender>();

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
