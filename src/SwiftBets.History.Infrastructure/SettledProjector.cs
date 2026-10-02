using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Settlement;
using SwiftBets.History.Application;

namespace SwiftBets.History.Infrastructure;

public sealed class SettledV2Projector(IHistoryStore store) : IEventHandler<CouponSettledV2>
{
    public Task HandleAsync(ConsumedEvent<CouponSettledV2> message, CancellationToken cancellationToken) => store.ProjectSettledAsync(message.Envelope.Payload, cancellationToken);
}
