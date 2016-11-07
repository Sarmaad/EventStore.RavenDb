using CommonDomain;
using System;
using System.Collections.Generic;


namespace EventStore.RavenDb.Persistence
{
    public sealed class RavenDbStoreRepository : IRepository, IDisposable
    {
        IEventStoreSession _session;

        public RavenDbStoreRepository(IEventStoreSession session)
        {
            _session = session;

        }
        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IAggregate
        {
            return GetById<TAggregate>(id, int.MaxValue);
        }
        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class,IAggregate
        {
            return _session.Load<TAggregate>(id);
        }
        public void Save(IAggregate aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            _session.Store(aggregate, commitId, updateHeaders);
        }

        public void Dispose()
        {
            _session.Dispose();
            _session = null;
        }
    }
}
