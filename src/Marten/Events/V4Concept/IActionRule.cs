namespace Marten.Events.V4Concept
{
    public interface IActionRule
    {
        AggregationAction Action { get; }
        bool DoesApply(IStreamFragment fragment);
    }
}