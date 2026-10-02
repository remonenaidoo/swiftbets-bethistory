using Npgsql;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Money;
using SwiftBets.Contracts.Placement;
using SwiftBets.Contracts.Settlement;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace SwiftBets.History.Infrastructure.Tests;

public sealed class HistoryProjectionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Settlement_arriving_before_its_placement_still_converges_to_a_complete_row()
    {
        var store = await StoreAsync();
        var (placed, settled) = Coupon();

        await store.ProjectSettledAsync(settled, CancellationToken.None);
        await store.ProjectPlacedAsync(placed, CancellationToken.None);

        var row = (await store.ListAsync(placed.PunterId, 10, CancellationToken.None)).ShouldHaveSingleItem();
        (row.Status, row.Stake, row.Payout, row.SettlementVersion).ShouldBe(("won", 1_000L, 2_000L, 1));
    }

    [Fact]
    public async Task Older_settlement_version_never_overwrites_a_newer_one()
    {
        var store = await StoreAsync();
        var (placed, settled) = Coupon();
        await store.ProjectPlacedAsync(placed, CancellationToken.None);

        await store.ProjectSettledAsync(settled with { SettlementVersion = 2, Outcome = CouponOutcome.Lost, TargetPayout = new Money(0, "ZAR") }, CancellationToken.None);
        await store.ProjectSettledAsync(settled, CancellationToken.None);

        var row = (await store.ListAsync(placed.PunterId, 10, CancellationToken.None)).ShouldHaveSingleItem();
        (row.Status, row.Payout).ShouldBe(("lost", 0L));
    }

    [Fact]
    public async Task A_cashed_out_settlement_reads_as_cashedOut_with_its_agreed_amount()
    {
        var store = await StoreAsync();
        var (placed, settled) = Coupon();
        await store.ProjectPlacedAsync(placed, CancellationToken.None);

        await store.ProjectSettledAsync(settled with { Outcome = CouponOutcome.CashedOut, TargetPayout = new Money(1_500, "ZAR") }, CancellationToken.None);

        var row = (await store.ListAsync(placed.PunterId, 10, CancellationToken.None)).ShouldHaveSingleItem();
        (row.Status, row.Payout, row.SettlementVersion).ShouldBe(("cashedOut", 1_500L, 1));
    }

    [Fact]
    public async Task A_banker_trixie_is_projected_as_a_system_coupon()
    {
        var store = await StoreAsync();
        var (placed, _) = Coupon();
        var zar = (long m) => new Money(m, "ZAR");
        var v2 = new CouponPlacedV2(placed.CouponId, placed.PunterId, zar(400), zar(9_000),
            [new CouponLegV2(Guid.NewGuid(), "b", "b-1x2", "home", 1.5m, 1, true), new CouponLegV2(Guid.NewGuid(), "x", "x-1x2", "home", 2m, 1, false),
             new CouponLegV2(Guid.NewGuid(), "y", "y-1x2", "draw", 3m, 1, false), new CouponLegV2(Guid.NewGuid(), "z", "z-1x2", "away", 4m, 1, false)],
            [new CouponBetV2(Guid.NewGuid(), "trixie", [2, 3], 4, zar(100), zar(400), zar(9_000))], DateTimeOffset.UtcNow);

        await store.ProjectPlacedAsync(v2, CancellationToken.None);

        var row = (await store.ListAsync(placed.PunterId, 10, CancellationToken.None)).ShouldHaveSingleItem();
        (row.BetType, row.Stake, row.PotentialPayout, row.TotalOdds).ShouldBe(("system", 400L, 9_000L, 22.5m));
    }

    private static (CouponPlacedV2, CouponSettledV2) Coupon()
    {
        var (couponId, punterId, betId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var zar = (long m) => new Money(m, "ZAR");
        return (
            new CouponPlacedV2(couponId, punterId, zar(1_000), zar(2_000), [new CouponLegV2(Guid.NewGuid(), "f", "f-1x2", "home", 2m, 1, false)],
                [new CouponBetV2(betId, "single", [1], 1, zar(1_000), zar(1_000), zar(2_000))], DateTimeOffset.UtcNow),
            new CouponSettledV2(couponId, punterId, 1, CouponOutcome.Won, zar(1_000), zar(2_000), [new BetSettlementV2(betId, CouponOutcome.Won, 1, 0, 0, zar(2_000))], DateTimeOffset.UtcNow));
    }

    private async Task<PostgresHistoryStore> StoreAsync()
    {
        var database = "history_" + Guid.NewGuid().ToString("N")[..10];
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = database }.ConnectionString;
        var entry = typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { $"--ConnectionStrings:SbHistory={connectionString}" }]);
        (entry is Task<int> task ? await task : (int)entry!).ShouldBe(0);
        return new PostgresHistoryStore(NpgsqlDataSource.Create(connectionString));
    }
}
