using System.Threading.Tasks;
using Marten.Internal.Sessions;

namespace Marten.Events.V4Concept
{
    public interface IAggregateCreator<TDoc, TEvent>
    {
        ValueTask<TDoc> Create(Event<TEvent> @event, QuerySession session);
    }
}
