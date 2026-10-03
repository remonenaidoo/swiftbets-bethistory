using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.History.Api;
using SwiftBets.History.Infrastructure;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-bethistory");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddSwiftBetsJwtBearer(builder.Configuration);
builder.Services.AddAuthorizationBuilder().AddPolicy(HistoryEndpoints.BetsRead, p => p.RequireClaim("perm", HistoryEndpoints.BetsRead));
builder.Services.AddCouponHistory(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.UseAuthentication();
app.UseAuthorization();
app.MapSwiftBetsOperationalEndpoints();
app.MapHistoryEndpoints();

await app.RunAsync();
return 0;

public partial class Program;
