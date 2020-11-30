namespace Marten.Events.V4Concept
{
    public enum AggregationAction
    {
        /// <summary>
        /// The action will have to be determined at runtime by
        /// fetching and building the aggregate
        /// </summary>
        Build,

        /// <summary>
        /// The aggregate document will be created
        /// as part of this batch of events
        /// </summary>
        Create,

        /// <summary>
        /// The aggregate will be deleted as part of this page
        /// </summary>
        Delete,

    }
}
