using System.Threading.RateLimiting;
using IHLDota2Bot.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Extensions.RateLimiting;

/// <summary>
/// The "player" rate-limit policy applied to the internet-facing player surface
/// (<c>/api/auth/*</c> and the me-endpoints): a fixed window per client IP, bound
/// from <see cref="PlayerRateLimitOptions"/>. When no limit is configured the policy
/// resolves to a no-op limiter, so the pipeline shape is identical in dev and prod
/// and turning the limit on is purely a configuration change.
/// </summary>
public static class PlayerRateLimiting
{
    public const string PolicyName = "player";

    public static IServiceCollection AddPlayerRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<PlayerRateLimitOptions>(
            configuration.GetSection(PlayerRateLimitOptions.SectionName)
        );

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(
                PolicyName,
                context =>
                {
                    var options = context
                        .RequestServices.GetRequiredService<IOptions<PlayerRateLimitOptions>>()
                        .Value;

                    if (options.PermitLimit <= 0)
                        return RateLimitPartition.GetNoLimiter("disabled");

                    // Partition per client IP so one abusive client can't starve the
                    // rest. Behind the edge proxy the connection IP is the tunnel's,
                    // which degrades to a shared bucket — still a functional brake.
                    var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(
                        key,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = options.PermitLimit,
                            Window = TimeSpan.FromSeconds(Math.Max(1, options.WindowSeconds)),
                            QueueLimit = 0,
                        }
                    );
                }
            );
        });

        return services;
    }
}
