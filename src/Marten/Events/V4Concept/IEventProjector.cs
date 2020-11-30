using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten.Events.Projections.Async;
using Marten.Internal.Operations;
using Marten.Internal.Sessions;

namespace Marten.Events.V4Concept
{
    public interface IEventProjector<T>
    {
        ValueTask<IReadOnlyList<IStorageOperation>> Project(Event<T> @event, QuerySession session, CancellationToken token);
    }

    public class NulloEventProjector<T> : IEventProjector<T>
    {
        public static readonly ValueTask<IReadOnlyList<IStorageOperation>> Empty = new ValueTask<IReadOnlyList<IStorageOperation>>(new IStorageOperation[0]);

        public ValueTask<IReadOnlyList<IStorageOperation>> Project(Event<T> @event, QuerySession session, CancellationToken token)
        {
            return Empty;
        }
    }

    public interface IEventProjectorFactory
    {
        IEventProjector<T> FindProjector<T>();
    }

}
