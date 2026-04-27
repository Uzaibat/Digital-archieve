using System.Text;
using System.Text.Json;
using FluentValidation;
using IDADRS.Application.Interfaces;
using IDADRS.Application.Services;
using IDADRS.Application.Validators;
using IDADRS.Core.Interfaces;
using IDADRS.Infrastructure.Persistence;
using IDADRS.NativeSearch;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        n => n.MigrationsAssembly("IDADRS.Infrastructure")));

builder.Services.AddScoped<IUnitOfWork, IDADRS.Infrastructure.Persistence.UnitOfWork>();
builder.Services.AddScoped<IJwtService,          JwtService>();
builder.Services.AddScoped<IAuthService,         AuthService>();
builder.Services.AddScoped<IDocumentService,     DocumentService>();
builder.Services.AddScoped<IUserService,         UserService>();
builder.Services.AddScoped<ICategoryService,     CategoryService>();
builder.Services.AddScoped<IReportService,       ReportService>();
builder.Services.AddScoped<IFileStorageService,  FileStorageService>();
builder.Services.AddNativeSearch();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(key),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("AdminOnly",     p => p.RequireRole("Admin"));
    opt.AddPolicy("ArchivistPlus", p => p.RequireRole("Admin", "Archivist"));
    opt.AddPolicy("AnyRole",       p => p.RequireRole("Admin", "Archivist", "User"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "IDADRS API",
        Version     = "v1",
        Description = "Intelligent Digital Archive & Document Retrieval System",
    });
    foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "*.xml"))
        c.IncludeXmlComments(xml, includeControllerXmlComments: true);

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

var configuredOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>()
    ?? Array.Empty<string>();

var envOrigins = (builder.Configuration["ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var allowedOrigins = configuredOrigins
    .Concat(envOrigins)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:3000", "http://localhost:5173" };
}

builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "CI")
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        var err = ctx.Features.Get<IExceptionHandlerFeature>();
        var msg = err?.Error?.Message ?? "Internal server error";
        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(new { success = false, data = (object?)null, message = msg })
        );
    });
});
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "IDADRS v1"));
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<IDADRS.API.Middleware.AccessLogMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
