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

// Admin session guard + session plumbing
builder.Services.AddSingleton<AdminGuard>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Assassins.Admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseSession();

app.Use(async (ctx, next) =>
{

    var path = ctx.Request.Path.Value ?? string.Empty;


    bool isPublic =
        path.StartsWith("/auth/login", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/auth/logout", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/signup", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/eliminations/report", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/error", StringComparison.OrdinalIgnoreCase);

    if (!isPublic)
    {
        var guard = ctx.RequestServices.GetRequiredService<AdminGuard>();
        if (!guard.IsAdmin(ctx))
        {
            var returnUrl = ctx.Request.Path + ctx.Request.QueryString;
            var loginUrl = $"/Auth/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
            ctx.Response.Redirect(loginUrl);
            return; 
        }
    }

    await next();
});

app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
