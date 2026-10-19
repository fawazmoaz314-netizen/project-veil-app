using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Veil.Infrastructure.Data;

/// <summary>
/// Used only by `dotnet ef` tooling. `migrations add` never opens a connection;
/// `database update` uses VEIL_DESIGN_CONNECTION when it is set.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VeilDbContext>
{
    public VeilDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("VEIL_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=veil_design;Username=veil;Password=design-time-only";

        var options = new DbContextOptionsBuilder<VeilDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new VeilDbContext(options);
    }
}
