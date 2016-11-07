using System;
using System.Collections.Generic;
using CommonDomain;

namespace EventStore.RavenDb
{
    public interface IEventStoreSession : IDisposable
    {
        TAggregate Load<TAggregate>(Guid id) where TAggregate : class, IAggregate;
        void Store(IAggregate aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders);
    }
}