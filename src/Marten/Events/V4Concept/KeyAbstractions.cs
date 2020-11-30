using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LamarCodeGeneration;
using Marten.Events.V4Concept.Aggregation;
using Marten.Events.V4Concept.CodeGeneration;
using Marten.Internal;

namespace Marten.Events.V4Concept
{
    public interface IProjectionBase
    {
        string ProjectionName { get; }
    }

    // TODO -- still support the "I build myself up aggregator pattern"
    public interface ILiveAggregator<T>
    {
        T Build(IReadOnlyList<IEvent> events, IQuerySession session);
        ValueTask<T> BuildAsync(IReadOnlyList<IEvent> events, IQuerySession session, CancellationToken cancellation);
    }


    public interface IInlineProjection : IProjectionBase
    {
        void Apply(IDocumentSession session, IReadOnlyList<EventStream> streams);

        Task ApplyAsync(IDocumentSession session, IReadOnlyList<EventStream> streams, CancellationToken cancellation);
    }

    public interface IProjectionShard
    {
        IProjection Parent { get; }
        string ShardName { get; }

    }

    public interface IAsyncProjection
    {
        Task<IV4EventPage> Fetch(long floor, long ceiling, CancellationToken token);
    }

    public interface IProjection: IProjectionBase
    {
        // TODO -- eliminate the dependency on DocumentStore
        void GenerateHandlerTypes(DocumentStore store, GeneratedAssembly assembly);

        IProjectionShard Shards();
        IInlineProjection ToInline();

        bool TryResolveLiveAggregator<T>(out ILiveAggregator<T> aggregator);
    }








}
