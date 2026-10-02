extern alias migrator;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Money;
using SwiftBets.Contracts.Placement;
using SwiftBets.Contracts.Settlement;
using SwiftBets.History.Application;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace SwiftBets.History.IntegrationTests;

public sealed class HistoryApiTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_punter_sees_only_their_own_coupons_and_an_operator_can_look_any_up()
    {
        await using var host = await HistoryHost.StartAsync(postgres);
        var (mine, theirs) = (Guid.NewGuid(), Guid.NewGuid());
        var store = host.Services.GetRequiredService<IHistoryStore>();
        var myCoupon = await PlaceAsync(store, mine);
        var theirCoupon = await PlaceAsync(store, theirs);

        using var punter = host.Client(mine, "Punter");
        var list = await punter.GetFromJsonAsync<JsonElement>("/me/coupons", TestContext.Current.CancellationToken);
        list.EnumerateArray().Select(c => c.GetProperty("couponId").GetGuid()).ShouldBe([myCoupon]);
        list[0].GetProperty("legs")[0].GetProperty("selectionId").GetString().ShouldBe("home");
        (await punter.GetAsync(new Uri($"/me/coupons/{theirCoupon}", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await punter.GetAsync(new Uri($"/admin/history/coupons/{theirCoupon}", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var operatorClient = host.Client(Guid.NewGuid(), "Operator");
        var lookedUp = await operatorClient.GetFromJsonAsync<JsonElement>($"/admin/history/coupons/{theirCoupon}", TestContext.Current.CancellationToken);
        lookedUp.GetProperty("punterId").GetGuid().ShouldBe(theirs);
        var byPunter = await operatorClient.GetFromJsonAsync<JsonElement>($"/admin/history/punters/{theirs}/coupons", TestContext.Current.CancellationToken);
        byPunter.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Open_coupons_filter_and_the_integrity_report_flags_stale_and_orphaned_rows()
    {
        await using var host = await HistoryHost.StartAsync(postgres);
        var punter = Guid.NewGuid();
        var store = host.Services.GetRequiredService<IHistoryStore>();
        var open = await PlaceAsync(store, punter);
        var settled = await PlaceAsync(store, punter);
        await store.ProjectSettledAsync(Settled(settled, punter, CouponOutcome.Won, 2_000), CancellationToken.None);
        var orphan = Guid.NewGuid();
        await store.ProjectSettledAsync(Settled(orphan, punter, CouponOutcome.Lost, 0), CancellationToken.None);

        using var punterClient = host.Client(punter, "Punter");
        var mine = await punterClient.GetFromJsonAsync<JsonElement>("/me/coupons?open=true", TestContext.Current.CancellationToken);
        mine.EnumerateArray().Select(c => c.GetProperty("couponId").GetGuid()).ShouldBe([open]);

        using var operatorClient = host.Client(Guid.NewGuid(), "Operator");
        var report = await operatorClient.GetFromJsonAsync<JsonElement>("/admin/history/integrity?staleHours=0&graceMinutes=0", TestContext.Current.CancellationToken);
        var findings = report.EnumerateArray().ToDictionary(f => f.GetProperty("couponId").GetGuid(), f => f.GetProperty("problem").GetString());
        findings.ShouldContainKeyAndValue(open, "staleOpen");
        findings.ShouldContainKeyAndValue(orphan, "settledWithoutPlacement");
        findings.ShouldNotContainKey(settled);
        (await punterClient.GetAsync(new Uri("/admin/history/integrity", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> PlaceAsync(IHistoryStore store, Guid punterId)
    {
        var couponId = Guid.NewGuid();
        await store.ProjectPlacedAsync(new CouponPlacedV2(couponId, punterId, new Money(1_000, "ZAR"), new Money(2_000, "ZAR"),
            [new CouponLegV2(Guid.NewGuid(), "f", "f-1x2", "home", 2m, 1, false)],
            [new CouponBetV2(Guid.NewGuid(), "single", [1], 1, new Money(1_000, "ZAR"), new Money(1_000, "ZAR"), new Money(2_000, "ZAR"))], DateTimeOffset.UtcNow), CancellationToken.None);
        return couponId;
    }

    private static CouponSettledV2 Settled(Guid couponId, Guid punterId, CouponOutcome outcome, long payout) =>
        new(couponId, punterId, 1, outcome, new Money(1_000, "ZAR"), new Money(payout, "ZAR"), [], DateTimeOffset.UtcNow);

    /// <summary>The real host against a fresh database, read side only.</summary>
    private sealed class HistoryHost(string connectionString) : WebApplicationFactory<Program>
    {
        private const string Issuer = "https://identity.history.test";
        private static readonly RsaSecurityKey Key = new(RSA.Create(2048)) { KeyId = "history.test" };

        public static async Task<HistoryHost> StartAsync(PostgresFixture postgres)
        {
            var name = "hist_" + Guid.NewGuid().ToString("N")[..10];
            await using (var server = new NpgsqlConnection(postgres.ConnectionString))
            {
                await server.ExecuteAsync($"CREATE DATABASE {name}");
            }

            var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = name }.ConnectionString;
            var entry = typeof(migrator::Program).Assembly.EntryPoint!.Invoke(null, [new[] { $"--ConnectionStrings:SbHistory={connectionString}" }]);
            (entry is Task<int> task ? await task : (int)entry!).ShouldBe(0);
            return new HistoryHost(connectionString);
        }

        public HttpClient Client(Guid subject, string role)
        {
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = "swiftbets",
                Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, subject.ToString()), new Claim("role", role)]),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256),
            });
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:SbHistory", connectionString);
            builder.UseSetting("History:RunProjector", "false");
            builder.UseSetting("Jwt:Authority", Issuer);
            builder.UseSetting("Jwt:RequireHttpsMetadata", "false");
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(Key);
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.TokenValidationParameters.ValidIssuer = Issuer;
                options.TokenValidationParameters.ValidAudience = "swiftbets";
            }));
        }
    }
}
