using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Placement;
using SwiftBets.History.Application;

namespace SwiftBets.History.Infrastructure;

public sealed class PlacedV2Projector(IHistoryStore store) : IEventHandler<CouponPlacedV2>
{
    public Task HandleAsync(ConsumedEvent<CouponPlacedV2> message, CancellationToken cancellationToken) => store.ProjectPlacedAsync(message.Envelope.Payload, cancellationToken);
}
