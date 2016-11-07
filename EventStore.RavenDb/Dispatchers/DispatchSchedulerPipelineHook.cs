using System;

namespace EventStore.RavenDb.Dispatchers
{
    class DispatchSchedulerPipelineHook : IPipelineHook
    {
        readonly IScheduleDispatches _scheduler;

        public DispatchSchedulerPipelineHook()
            : this(null)
        {
        }
        public DispatchSchedulerPipelineHook(IScheduleDispatches scheduler)
        {
            this._scheduler = scheduler ?? new NullDispatcher();
        }
        public void Dispose()
        {
            _scheduler.Dispose();
            GC.SuppressFinalize(this);
        }
        public Commit Select(Commit committed)
        {
            return committed;
        }
        public virtual bool PreCommit(Commit attempt)
        {
            return true;
        }
        public void PostCommit(Commit committed, string databaseName)
        {
            if (committed != null)
                _scheduler.ScheduleDispatch(committed, databaseName);
        }
    }
}
