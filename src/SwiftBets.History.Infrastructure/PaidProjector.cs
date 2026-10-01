using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Payout;
using SwiftBets.History.Application;

namespace SwiftBets.History.Infrastructure;

public sealed class PaidProjector(IHistoryStore store) : IEventHandler<PayoutCompletedV1>
{
    public Task HandleAsync(ConsumedEvent<PayoutCompletedV1> message, CancellationToken cancellationToken) => store.ProjectPaidAsync(message.Envelope.Payload, cancellationToken);
}
