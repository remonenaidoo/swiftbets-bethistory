using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Settlement;
using SwiftBets.History.Application;

namespace SwiftBets.History.Infrastructure;

public sealed class SettledProjector(IHistoryStore store) : IEventHandler<CouponSettledV1>
{
    public Task HandleAsync(ConsumedEvent<CouponSettledV1> message, CancellationToken cancellationToken) => store.ProjectSettledAsync(message.Envelope.Payload, cancellationToken);
}
