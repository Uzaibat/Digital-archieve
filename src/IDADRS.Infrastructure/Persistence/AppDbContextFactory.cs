using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace IDADRS.Infrastructure.Persistence;

/// <summary>Used by EF Core CLI tooling (dotnet ef migrations add).</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var conn = config.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=idadrs_dev;Username=postgres;Password=postgres";

        var opt = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn, o => o.MigrationsAssembly("IDADRS.Infrastructure"))
            .Options;

        return new AppDbContext(opt);
    }
}
