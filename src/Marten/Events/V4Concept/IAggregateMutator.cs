using System.Threading.Tasks;
using Marten.Internal.Sessions;

namespace Marten.Events.V4Concept
{
    public interface IAggregateMutator<TDoc, TEvent>
    {
        ValueTask<TDoc> Apply(TDoc aggregate, Event<TEvent> @event, QuerySession session);
    }
}
