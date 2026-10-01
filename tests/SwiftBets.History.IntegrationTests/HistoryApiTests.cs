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

    private static async Task<Guid> PlaceAsync(IHistoryStore store, Guid punterId)
    {
        var couponId = Guid.NewGuid();
        await store.ProjectPlacedAsync(new CouponPlacedV1(couponId, punterId, BetType.Single, new Money(1_000, "ZAR"), 2m, new Money(2_000, "ZAR"),
            [new CouponLegV1(Guid.NewGuid(), "f", "f-1x2", "home", 2m, 1)], DateTimeOffset.UtcNow), CancellationToken.None);
        return couponId;
    }

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
