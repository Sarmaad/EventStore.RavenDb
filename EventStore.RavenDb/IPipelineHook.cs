using System;

namespace EventStore.RavenDb
{
    public interface IPipelineHook : IDisposable
    {
        Commit Select(Commit committed);
        bool PreCommit(Commit attempt);
        void PostCommit(Commit committed, string databaseName);
    }
}
