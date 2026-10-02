using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SwiftBets.History.Infrastructure;

namespace SwiftBets.History.IntegrationTests;

public sealed class RegistrationTests
{
    [Fact]
    public void Every_projector_resolves_when_the_projector_runs()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SbHistory"] = "Host=127.0.0.1;Database=x;Username=x;Password=x",
            ["Kafka:BootstrapServers"] = "127.0.0.1:1",
            ["Kafka:Environment"] = "test",
            ["Kafka:ClientId"] = "history.tests",
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddCouponHistory(configuration);

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IHostedService>().Count().ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void With_integrity_off_every_registration_validates_on_build()
    {
        using var provider = Build(new Dictionary<string, string?>());

        provider.GetService<SwiftBets.History.Application.Integrity.RunIntegrityCheckHandler>().ShouldBeNull();
    }

    [Fact]
    public void With_integrity_on_the_check_and_its_worker_resolve()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["Integrity:Enabled"] = "true",
            ["Integrity:Clients:PlacementAddress"] = "http://placement:8080",
            ["Integrity:Clients:SettlementAddress"] = "http://settlement:8080",
            ["Integrity:Clients:PayoutAddress"] = "http://payout:8080",
            ["ServiceIdentity:TokenEndpoint"] = "http://identity:8080/auth/token",
            ["ServiceIdentity:ClientId"] = "bethistory",
            ["ServiceIdentity:ClientSecret"] = "secret",
        });

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<SwiftBets.History.Application.Integrity.RunIntegrityCheckHandler>().ShouldNotBeNull();
        provider.GetServices<IHostedService>().ShouldContain(h => h.GetType().Name == "IntegrityWorker");
    }

    /// <summary>Validates every registration up front, as the Development host does.</summary>
    private static ServiceProvider Build(Dictionary<string, string?> extra)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SbHistory"] = "Host=127.0.0.1;Database=x;Username=x;Password=x",
            ["Kafka:BootstrapServers"] = "127.0.0.1:1",
            ["Kafka:Environment"] = "test",
            ["Kafka:ClientId"] = "history.tests",
        };
        foreach (var (key, value) in extra)
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration).AddSingleton(TimeProvider.System);
        services.AddCouponHistory(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
