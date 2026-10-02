using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.History.Application.Integrity;

namespace SwiftBets.History.Infrastructure.Integrity;

/// <summary>Runs the cross-store check on a fixed interval; a failed run is logged and the next interval tries again.</summary>
public sealed partial class IntegrityWorker(IServiceScopeFactory scopes, IOptions<IntegrityOptions> options, TimeProvider time, ILogger<IntegrityWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.IntervalMinutes), time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var run = await scope.ServiceProvider.GetRequiredService<RunIntegrityCheckHandler>().HandleAsync(stoppingToken);
                if (run.Findings.Count > 0)
                {
                    LogFindings(run.Findings.Count, run.CouponsChecked, run.RunId);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogRunFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bet-history integrity: {Findings} findings across {Checked} coupons (run {RunId})")]
    private partial void LogFindings(int findings, int @checked, Guid runId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bet-history integrity run failed; retrying next interval")]
    private partial void LogRunFailed(Exception exception);
}
