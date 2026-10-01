using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Payout;
using SwiftBets.Contracts.Placement;
using SwiftBets.Contracts.Settlement;
using SwiftBets.History.Application;

namespace SwiftBets.History.Infrastructure;

public static class HistoryRegistration
{
    /// <summary>The projectors run in this service by default; <c>History:RunProjector=false</c> leaves only the read side.</summary>
    public static IServiceCollection AddCouponHistory(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings:SbHistory"] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("Configuration 'ConnectionStrings:SbHistory' is required.");
        services.AddKeyedSingleton("history", (_, _) => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IHistoryStore>(sp => new PostgresHistoryStore(sp.GetRequiredKeyedService<NpgsqlDataSource>("history")));
        if (configuration.GetValue("History:RunProjector", true))
        {
            services.AddKafkaMessaging(configuration);
            services.AddKafkaConsumer<CouponPlacedV1, PlacedProjector>(Topics.CouponPlaced, "swiftbets.history.placed");
            services.AddKafkaConsumer<CouponPlacedV2, PlacedV2Projector>(Topics.CouponPlacedV2, "swiftbets.history.placed-v2");
            services.AddKafkaConsumer<CouponSettledV1, SettledProjector>(Topics.CouponSettled, "swiftbets.history.settled");
            services.AddKafkaConsumer<PayoutCompletedV1, PaidProjector>(Topics.PayoutCompleted, "swiftbets.history.paid");
        }

        return services;
    }
}
