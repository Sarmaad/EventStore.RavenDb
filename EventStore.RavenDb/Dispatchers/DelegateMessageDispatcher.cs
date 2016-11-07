using System;

namespace EventStore.RavenDb.Dispatchers
{
    public class DelegateMessageDispatcher : IDispatchCommits
    {
        private readonly Action<Commit> _dispatch;

        public DelegateMessageDispatcher(Action<Commit> dispatch)
        {
            _dispatch = dispatch;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public virtual void Dispatch(Commit commit)
        {
            _dispatch(commit);
        }
    }
}