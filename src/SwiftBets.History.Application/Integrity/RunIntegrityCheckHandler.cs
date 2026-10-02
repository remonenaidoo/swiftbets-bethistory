using Microsoft.Extensions.Options;

namespace SwiftBets.History.Application.Integrity;

/// <summary>One run: placed coupons in the window, their history rows, settlements and payouts, compared and recorded.</summary>
public sealed class RunIntegrityCheckHandler(IIntegritySources sources, IHistoryStore history, IOptions<IntegrityOptions> options, TimeProvider time)
{
    private const int Batch = 500;

    public async Task<IntegrityRun> HandleAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var grace = TimeSpan.FromMinutes(options.Value.GraceMinutes);
        var (from, to) = (now - TimeSpan.FromHours(options.Value.WindowHours), now - grace);
        var placed = await sources.PlacedAsync(from, to, cancellationToken);
        var findings = new List<CrossStoreFinding>();
        foreach (var chunk in placed.Chunk(Batch))
        {
            var ids = chunk.Select(c => c.CouponId).ToList();
            var rows = await history.GetManyAsync(ids, cancellationToken);
            var settled = await sources.SettledAsync(ids, cancellationToken);
            var paid = await sources.PaidAsync(ids, cancellationToken);
            findings.AddRange(IntegrityCheck.Compare(chunk, rows.ToDictionary(r => r.CouponId), settled, paid, now - grace));
        }

        var run = new IntegrityRun(Guid.CreateVersion7(), now, from, to, placed.Count, findings);
        await history.RecordIntegrityRunAsync(run, cancellationToken);
        return run;
    }
}
