using System.Threading;
using Raven.Client;

namespace EventStore.RavenDb.Dispatchers
{
    internal class AsynchronousDispatchScheduler : SynchronousDispatchScheduler
    {
        public AsynchronousDispatchScheduler(IDispatchCommits dispatcher, IDocumentStore store)
            : base(dispatcher, store)
        {
        }

        public override void ScheduleDispatch(Commit commit, string databaseName)
        {
            ThreadPool.QueueUserWorkItem(x => Callback(commit, databaseName));
        }
        void Callback(Commit commit, string databaseName)
        {
            base.ScheduleDispatch(commit, databaseName);
        }
    }
}
