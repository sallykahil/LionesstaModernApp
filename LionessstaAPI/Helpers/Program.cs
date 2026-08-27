using LionessstaAPI.Data;
using LionessstaAPI.Helpers;
using LionessstaAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
builder.Services.AddCors();

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
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
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
app.UseStaticFiles();
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();