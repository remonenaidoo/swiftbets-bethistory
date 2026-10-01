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
}
