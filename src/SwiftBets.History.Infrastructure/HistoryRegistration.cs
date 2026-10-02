using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Resilience;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Payout;
using SwiftBets.Contracts.Placement;
using SwiftBets.Contracts.Settlement;
using SwiftBets.History.Application;
using SwiftBets.History.Application.Integrity;
using SwiftBets.History.Infrastructure.Integrity;

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
            services.AddKafkaConsumer<CouponPlacedV2, PlacedV2Projector>(Topics.CouponPlacedV2, "swiftbets.history.placed-v2");
            services.AddKafkaConsumer<CouponSettledV2, SettledV2Projector>(Topics.CouponSettledV2, "swiftbets.history.settled-v2");
            services.AddKafkaConsumer<PayoutCompletedV1, PaidProjector>(Topics.PayoutCompleted, "swiftbets.history.paid");
        }

        services.AddValidatedOptions<IntegrityOptions>(configuration, IntegrityOptions.SectionName);
        services.AddScoped<RunIntegrityCheckHandler>();
        if (configuration.GetValue("Integrity:Enabled", false))
        {
            services.AddValidatedOptions<IntegrityClients>(configuration, IntegrityClients.SectionName);
            services.AddClientCredentials(configuration);
            foreach (var (name, address) in new (string, Func<IntegrityClients, string>)[]
            {
                (HttpIntegritySources.Placement, c => c.PlacementAddress),
                (HttpIntegritySources.Settlement, c => c.SettlementAddress),
                (HttpIntegritySources.Payout, c => c.PayoutAddress),
            })
            {
                services.AddHttpClient(name, (sp, http) => http.BaseAddress = new Uri(address(sp.GetRequiredService<IOptions<IntegrityClients>>().Value).TrimEnd('/') + "/"))
                    .AddIdempotentResilience();
            }

            services.AddSingleton<IIntegritySources, HttpIntegritySources>();
            services.AddHostedService<IntegrityWorker>();
        }

        return services;
    }
}
