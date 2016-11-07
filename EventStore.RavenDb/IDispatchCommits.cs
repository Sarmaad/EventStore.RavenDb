using System;

namespace EventStore.RavenDb
{
    public interface IDispatchCommits : IDisposable
    {
        void Dispatch(Commit commit);
    }
}
