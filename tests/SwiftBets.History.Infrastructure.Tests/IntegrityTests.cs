using Npgsql;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.History.Application;
using SwiftBets.History.Application.Integrity;

namespace SwiftBets.History.Infrastructure.Tests;

public sealed class IntegrityTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Missing_lagging_and_mismatched_coupons_are_each_reported()
    {
        var (missing, lagging, underpaid) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var history = new[] { Row(lagging, settlementVersion: 1, paid: 2_000), Row(underpaid, settlementVersion: 2, paid: 0) }.ToDictionary(r => r.CouponId);

        var findings = IntegrityCheck.Compare(
            [Placed(missing), Placed(lagging), Placed(underpaid)], history,
            [new(lagging, 2, "lost", 0, Now.AddHours(-1)), new(underpaid, 2, "won", 2_000, Now.AddHours(-1))],
            [new(underpaid, 2_000, 2)], Now.AddMinutes(-10));

        findings.Select(f => (f.CouponId, f.Problem)).ShouldBe([(missing, "missingInHistory"), (lagging, "settlementBehind"), (underpaid, "paidMismatch")], ignoreOrder: true);
    }

    [Fact]
    public void A_matching_coupon_and_a_settlement_still_in_flight_are_not_findings()
    {
        var (matching, inFlight) = (Guid.NewGuid(), Guid.NewGuid());
        var history = new[] { Row(matching, settlementVersion: 1, paid: 2_000), Row(inFlight, settlementVersion: 0, paid: 0) }.ToDictionary(r => r.CouponId);

        var findings = IntegrityCheck.Compare(
            [Placed(matching), Placed(inFlight)], history,
            [new(matching, 1, "won", 2_000, Now.AddHours(-1)), new(inFlight, 1, "won", 2_000, Now.AddMinutes(-2))],
            [new(matching, 2_000, 1)], Now.AddMinutes(-10));

        findings.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_recorded_run_reads_back_as_the_latest_with_its_findings()
    {
        var store = await StoreAsync();
        var coupon = Guid.NewGuid();
        await store.RecordIntegrityRunAsync(new IntegrityRun(Guid.CreateVersion7(), Now.AddMinutes(-30), Now.AddDays(-1), Now.AddMinutes(-40), 3, []), CancellationToken.None);
        var latest = new IntegrityRun(Guid.CreateVersion7(), Now, Now.AddDays(-1), Now.AddMinutes(-10), 7, [new(coupon, "missingInHistory", "x")]);

        await store.RecordIntegrityRunAsync(latest, CancellationToken.None);

        var read = (await store.GetLatestIntegrityRunAsync(CancellationToken.None)).ShouldNotBeNull();
        (read.RunId, read.CouponsChecked, read.Findings.ShouldHaveSingleItem().CouponId).ShouldBe((latest.RunId, 7, coupon));
    }

    private static PlacedFact Placed(Guid id) => new(id, 1_000, 2_000, "ZAR", Now.AddHours(-2));

    private static CouponHistoryRow Row(Guid id, int settlementVersion, long paid) =>
        new(id, "open", "single", 1_000, "ZAR", 2m, 2_000, null, Now.AddHours(-2).UtcDateTime, settlementVersion, null, paid, Now.UtcDateTime, Guid.NewGuid());

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
