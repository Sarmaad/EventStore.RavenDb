using System.Diagnostics;

namespace EventStore.RavenDb.Infrastructure
{
    internal static class PrivateReflectionDynamicObjectExtensions
    {
        [DebuggerStepThrough]
        public static dynamic AsDynamic(this object o)
        {
            return PrivateReflectionDynamicObject.WrapObjectIfNeeded(o);
        }
    }
}