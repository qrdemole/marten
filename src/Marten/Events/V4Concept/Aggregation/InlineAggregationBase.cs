using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten.Internal.Operations;

namespace Marten.Events.V4Concept.Aggregation
{
    public abstract class InlineAggregationBase<TDoc, TId>: IInlineProjection
    {
        public InlineAggregationBase(string projectionName)
        {
            ProjectionName = projectionName;
        }

        public string ProjectionName { get; }

        public void Apply(IDocumentSession session, IReadOnlyList<EventStream> streams)
        {
            ApplyAsync(session, streams, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task ApplyAsync(IDocumentSession session, IReadOnlyList<EventStream> streams,
            CancellationToken cancellation)
        {
            foreach (var stream in streams)
            {
                var operation = await DetermineOperation(session, stream, cancellation);
                session.QueueOperation(operation);
            }
        }

        public abstract Task<IStorageOperation> DetermineOperation(IDocumentSession session, EventStream stream, CancellationToken cancellation);
    }
}
