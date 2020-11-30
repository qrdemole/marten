namespace Marten.Events.V4Concept
{
    public interface IMutatorCollection<TDoc>
    {
        // TODO -- Crap. Use some Co/contra variance to deal with sub types and
        // interfaces for the mutators and creators

        // TODO -- Double crap, it could be multiples!!
        // What if we make it lazy resolved here?

        bool TryFindMutator<TEvent>(out IAggregateMutator<TDoc, TEvent> mutator);
        bool TryFindCreator<TEvent>(out IAggregateCreator<TDoc, TEvent> creator);
    }
}