using Microsoft.EntityFrameworkCore;
using MultiTenantSaaS.Data;
using MultiTenantSaaS.Middleware;
using MultiTenantSaaS.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Hardcode the port to 80
builder.WebHost.UseUrls("http://*:80");


// Configure lowercase URLs for consistency with frontend
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// 1. Add Services  - use snake_case JSON so that frontend snake_case fields (deposit_price, linked_deposit_id) match directly
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Tenant Service (Scoped per request)
builder.Services.AddScoped<ITenantService, TenantService>();

// 3. Database Configuration
var connString = builder.Configuration.GetConnectionString("DefaultConnection");

// Master DB Context (Public Schema)
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseNpgsql(connString, npgsqlOptions => {
        npgsqlOptions.CommandTimeout(60);
    }));

// App DB Context (Tenant Schemas)
builder.Services.AddScoped<TenantSchemaInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<TenantSchemaInterceptor>();
    options.UseNpgsql(connString, npgsqlOptions => {
        npgsqlOptions.CommandTimeout(60);
    })
    .AddInterceptors(interceptor);
});

// 4. Auth & CORS
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_1234567890123456";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });


var app = builder.Build();

// 5. Pipeline
// Fix for proxy (Traefik) handling
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MultiTenant API V1");
    c.RoutePrefix = "swagger"; 
});

// Root redirect to Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseCors("AllowAll");

// Custom Middleware MUST be before UseAuthorization
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Ensure Master DB (public schema) is created on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the master database.");
    }
}

app.Run();
