using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veil.Contracts;
using Veil.Domain.Users;
using Veil.Infrastructure.Data;

namespace Veil.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVeilInfrastructure(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Veil is not configured. Set it via the ConnectionStrings__Veil environment variable.");
        }

        services.AddDbContext<VeilDbContext>(options => options.UseNpgsql(connectionString));

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.";

                // Length over composition rules (NIST SP 800-63B guidance).
                options.Password.RequiredLength = Limits.PasswordMinLength;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<VeilDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
