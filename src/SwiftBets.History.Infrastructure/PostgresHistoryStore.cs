using System.Text.Json;
using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Contracts.Payout;
using SwiftBets.Contracts.Placement;
using SwiftBets.Contracts.Serialization;
using SwiftBets.Contracts.Settlement;
using SwiftBets.History.Application;

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

    public async Task<IReadOnlyList<CouponHistoryRow>> ListAsync(Guid punterId, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<CouponHistoryRow>(new CommandDefinition(Sql.Get("History.List"), new { PunterId = punterId, Limit = Math.Clamp(limit, 1, 100) }, cancellationToken: cancellationToken))];
    }

    public async Task<CouponHistoryRow?> GetAsync(Guid couponId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CouponHistoryRow>(new CommandDefinition(Sql.Get("History.Get"), new { CouponId = couponId }, cancellationToken: cancellationToken));
    }
}
