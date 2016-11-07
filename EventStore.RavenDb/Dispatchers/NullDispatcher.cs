namespace EventStore.RavenDb.Dispatchers
{
    internal sealed class NullDispatcher : IScheduleDispatches, IDispatchCommits
    {
        public void Dispose()
        {

        }

        public void ScheduleDispatch(Commit commit, string databaseName)
        {
            Dispatch(commit);
        }

        public void Dispatch(Commit commit)
        {

        }
    }
}