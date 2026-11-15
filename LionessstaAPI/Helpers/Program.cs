using LionessstaAPI.Data;
using LionessstaAPI.Helpers;
using LionessstaAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

// One-off helper, not part of the running site:
//   dotnet run -- hash-password "your-new-password"
// prints the value to store in the Admin:PasswordHash setting, then exits.
if (args.Length >= 2 && args[0] == "hash-password")
{
    Console.WriteLine(PasswordHelper.Hash(args[1]));
    return;
}

var builder = WebApplication.CreateBuilder(args);

// ── DATABASE
// EnableRetryOnFailure absorbs Azure SQL's transient errors (e.g. error 40613,
// "database not currently available" -- common right after a serverless-tier
// database wakes up from being paused) by retrying instead of throwing.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// ── SERVICES
builder.Services.AddScoped<IBlobService, BlobService>();
builder.Services.AddScoped<JwtHelper>();

// Products/categories change roughly once a day, but every visitor's page
// load reads them -- caching the list endpoints in memory means only the
// first request after a cache miss touches SQL; everyone else gets an
// instant in-process response. Controllers invalidate these entries the
// moment something is actually created/updated/deleted.
builder.Services.AddMemoryCache();

// ── JWT AUTHENTICATION
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── LOGIN RATE LIMIT
// The admin login is the only thing protecting uploads and deletes, and the
// login page is public. Allow each visitor (by IP address) 5 attempts per
// minute; anything beyond that gets a 429 until the minute is up.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many login attempts. Please wait a minute and try again." },
            cancellationToken);
    };
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Apply any pending EF Core migrations on startup. This is what keeps the
// deployed database schema in sync automatically -- without it, a migration
// added locally (e.g. new Categories table) never reaches Azure SQL unless
// someone remembers to run `dotnet ef database update` against it by hand.
//
// Wrapped in try/catch on purpose: if the database is temporarily unreachable
// (e.g. mid wake-up from being paused) this must NOT crash the whole app --
// that would take down static pages and every other route too. Worst case,
// DB-backed endpoints keep failing individually (as before this feature
// existed) until the next successful restart retries the migration.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Migrate() must run through the execution strategy when EnableRetryOnFailure
    // is configured -- calling it directly throws "the configured execution
    // strategy does not support user-initiated transactions", because Migrate()
    // wraps each migration in its own transaction and the retry wrapper needs to
    // own that transaction to retry it safely as a whole unit.
    var strategy = db.Database.CreateExecutionStrategy();
    strategy.Execute(() => db.Database.Migrate());
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Startup migration failed -- app will still start; DB-backed endpoints may error until this succeeds on a later restart.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Site artwork and the intro video rarely change (and the pages add
        // ?v=N to their URLs when they do), so let browsers keep them for a
        // week instead of re-downloading on every visit. The HTML pages are
        // left alone so a new deploy shows up immediately.
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/videos/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=604800";
        }
    }
});

// No CORS policy on purpose: the storefront and admin pages are served from
// this same app, so no other website needs (or gets) browser access to the API.
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();