using System.Security.Claims;
using ForikAuction.Components;
using ForikAuction.Data;
using ForikAuction.Data.Entities;
using ForikAuction.Hubs;
using ForikAuction.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<AuctionService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<TalentService>();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// показывать подробности ошибок circuit'а (на время отладки)
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(o => o.DetailedErrors = true);
builder.Services.AddRazorPages();          // для эндпоинтов входа/выхода
builder.Services.AddSignalR();
builder.Services.AddSingleton<SpinStateStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddGoogle(o =>
    {
        o.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        o.SaveTokens = false;
        // при первом входе заводим/обновляем пользователя в БД
        o.Events.OnCreatingTicket = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var sub = ctx.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var email = ctx.Principal!.FindFirstValue(ClaimTypes.Email) ?? "";
            var name = ctx.Principal!.FindFirstValue(ClaimTypes.Name) ?? email;
            string? pic = ctx.Principal!.FindFirstValue("picture");
            if (pic is null && ctx.User.TryGetProperty("picture", out var picEl)) pic = picEl.GetString();

            var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleSubject == sub);
            if (user is null)
            {
                user = new AppUser { GoogleSubject = sub, Email = email, DisplayName = name, AvatarUrl = pic };
                db.Users.Add(user);
            }
            else { user.DisplayName = name; user.Email = email; user.AvatarUrl = pic; }
            await db.SaveChangesAsync();

            var id = (ClaimsIdentity)ctx.Principal!.Identity!;
            id.AddClaim(new Claim("app_uid", user.Id.ToString()));
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// создаём БД при первом старте (для MVP — без миграций)
using (var scope = app.Services.CreateScope())
{
    // SQLite сам не создаёт каталог для файла БД — создадим его, чтобы не падать на хостинге.
    var cs = builder.Configuration.GetConnectionString("Default");
    if (!string.IsNullOrWhiteSpace(cs))
    {
        var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(cs).DataSource;
        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            var fullPath = Path.IsPathRooted(dataSource)
                ? dataSource
                : Path.Combine(app.Environment.ContentRootPath, dataSource);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
    }
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// --- эндпоинты входа/выхода (Blazor-компоненты не могут сами делать redirect-challenge) ---
app.MapGet("/auth/login", (HttpContext http, string? returnUrl) =>
    Results.Challenge(
        new() { RedirectUri = returnUrl ?? "/" },
        new[] { GoogleDefaults.AuthenticationScheme }));

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapHub<AuctionHub>("/hubs/auction");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
