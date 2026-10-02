using System.Text.Json;
using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Contracts.Payout;
using SwiftBets.Contracts.Placement;
using SwiftBets.Contracts.Serialization;
using SwiftBets.Contracts.Settlement;
using SwiftBets.History.Application;
using SwiftBets.History.Application.Integrity;

namespace SwiftBets.History.Infrastructure;

public sealed class PostgresHistoryStore(NpgsqlDataSource dataSource) : IHistoryStore
{
    private static readonly SqlResources Sql = SqlResources.For<PostgresHistoryStore>();

    public async Task ProjectPlacedAsync(CouponPlacedV1 placed, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("History.Placed"), new
        {
            placed.CouponId, placed.PunterId, BetType = placed.BetType.ToString().ToLowerInvariant(), Stake = placed.Stake.MinorUnits, placed.Stake.Currency,
            placed.TotalOdds, PotentialPayout = placed.PotentialPayout.MinorUnits, Legs = JsonSerializer.Serialize(placed.Legs, ContractJson.Options), placed.PlacedAt,
        }, cancellationToken: cancellationToken));
    }

    public async Task ProjectPlacedAsync(CouponPlacedV2 placed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(placed);
        var system = placed.Bets.Count != 1 || placed.Bets[0].Lines != 1 || placed.Legs.Any(l => l.IsBanker);
        var betType = system ? BetType.System : placed.Legs.Count == 1 ? BetType.Single : BetType.Accumulator;
        var odds = placed.TotalStake.MinorUnits == 0 ? 0m : decimal.Round((decimal)placed.PotentialPayout.MinorUnits / placed.TotalStake.MinorUnits, 6, MidpointRounding.ToZero);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("History.Placed"), new
        {
            placed.CouponId, placed.PunterId, BetType = betType.ToString().ToLowerInvariant(), Stake = placed.TotalStake.MinorUnits, placed.TotalStake.Currency,
            TotalOdds = odds, PotentialPayout = placed.PotentialPayout.MinorUnits, Legs = JsonSerializer.Serialize(placed.Legs, ContractJson.Options), placed.PlacedAt,
        }, cancellationToken: cancellationToken));
    }

    public async Task ProjectSettledAsync(CouponSettledV1 settled, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("History.Settled"), new
        {
            settled.CouponId, settled.PunterId, Status = settled.Outcome.ToString().ToLowerInvariant(), settled.TargetPayout.Currency,
            Version = settled.SettlementVersion, Payout = settled.TargetPayout.MinorUnits, settled.SettledAt,
        }, cancellationToken: cancellationToken));
    }

    public async Task ProjectPaidAsync(PayoutCompletedV1 paid, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("History.Paid"), new
        {
            paid.CouponId, paid.PunterId, paid.PaidToDate.Currency, Version = paid.SettlementVersion, PaidToDate = paid.PaidToDate.MinorUnits,
        }, cancellationToken: cancellationToken));
    }

    public Task<IReadOnlyList<CouponHistoryRow>> ListAsync(Guid punterId, int limit, CancellationToken cancellationToken) =>
        ListAsync(punterId, limit, false, cancellationToken);

    public Task<IReadOnlyList<CouponHistoryRow>> ListOpenAsync(Guid punterId, int limit, CancellationToken cancellationToken) =>
        ListAsync(punterId, limit, true, cancellationToken);

    public async Task<IReadOnlyList<IntegrityFinding>> FindIntegrityProblemsAsync(int staleHours, int graceMinutes, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<IntegrityFinding>(new CommandDefinition(Sql.Get("History.Integrity"), new
        {
            StaleHours = Math.Max(staleHours, 0), GraceMinutes = Math.Max(graceMinutes, 0),
        }, cancellationToken: cancellationToken))];
    }

    public async Task<IReadOnlyList<CouponHistoryRow>> GetManyAsync(IReadOnlyList<Guid> couponIds, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<CouponHistoryRow>(new CommandDefinition(Sql.Get("History.GetMany"), new { CouponIds = couponIds.ToArray() }, cancellationToken: cancellationToken))];
    }

    public async Task RecordIntegrityRunAsync(IntegrityRun run, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("History.RecordRun"), new
        {
            run.RunId, StartedAt = run.StartedAt.UtcDateTime, WindowFrom = run.WindowFrom.UtcDateTime, WindowTo = run.WindowTo.UtcDateTime, run.CouponsChecked,
            CouponIds = run.Findings.Select(f => f.CouponId).ToArray(),
            Problems = run.Findings.Select(f => f.Problem).ToArray(),
            Details = run.Findings.Select(f => f.Detail).ToArray(),
        }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IntegrityRun?> GetLatestIntegrityRunAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(Sql.Get("History.LatestRun"), cancellationToken: cancellationToken));
        var run = await results.ReadSingleOrDefaultAsync<RunRow>();
        var findings = (await results.ReadAsync<CrossStoreFinding>()).ToList();
        return run is null ? null : new IntegrityRun(run.RunId, run.StartedAt, run.WindowFrom, run.WindowTo, run.CouponsChecked, findings);
    }

    private sealed record RunRow(Guid RunId, DateTime StartedAt, DateTime WindowFrom, DateTime WindowTo, int CouponsChecked);

    private async Task<IReadOnlyList<CouponHistoryRow>> ListAsync(Guid punterId, int limit, bool openOnly, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<CouponHistoryRow>(new CommandDefinition(Sql.Get("History.List"), new
        {
            PunterId = punterId, Limit = Math.Clamp(limit, 1, 100), OpenOnly = openOnly,
        }, cancellationToken: cancellationToken))];
    }

    public async Task<CouponHistoryRow?> GetAsync(Guid couponId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CouponHistoryRow>(new CommandDefinition(Sql.Get("History.Get"), new { CouponId = couponId }, cancellationToken: cancellationToken));
    }
}
