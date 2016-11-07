
namespace EventStore.RavenDb.Conversion
{
    public interface IUpconvertEvents<TSource, TTarget>
        where TSource : class
        where TTarget : class
    {
        TTarget Convert(TSource sourceEvent);
    }
}
