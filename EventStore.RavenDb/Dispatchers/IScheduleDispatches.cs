using System;

namespace EventStore.RavenDb.Dispatchers
{
    interface IScheduleDispatches : IDisposable
    {
        void ScheduleDispatch(Commit commit, string databaseName);
    }
}