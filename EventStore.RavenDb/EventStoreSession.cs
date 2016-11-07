using CommonDomain;
using EventStore.RavenDb.Infrastructure;
using Raven.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.RavenDb
{
    internal sealed class EventStoreSession : IEventStoreSession
    {
        IDocumentStore _store;
        List<IPipelineHook> _pipelineHooks;
        readonly string _databaseName;


        internal EventStoreSession(IDocumentStore store, IEnumerable<IPipelineHook> pipelineHooks, string databaseName)
        {
            _store = store;
            _pipelineHooks = pipelineHooks.ToList();
            _databaseName = databaseName;
        }

        public TAggregate Load<TAggregate>(Guid id) where TAggregate : class, IAggregate
        {
            using (var session = string.IsNullOrWhiteSpace(_databaseName) ? _store.OpenSession() : _store.OpenSession(_databaseName))
            {
                session.Advanced.AllowNonAuthoritativeInformation = false;

                var stream = session.Load<EventStream>(id.ToStringId<EventStream>());
                if (stream == null) return null;

                var instance = (TAggregate)Activator.CreateInstance(typeof(TAggregate));

                foreach (var commit in stream.Commits.Select(x => x.Value).OrderBy(x => x.CommitStamp))
                {
                    var filtered = commit;
                    foreach (var hook in _pipelineHooks.Where(x => (filtered = x.Select(filtered)) == null))
                    {
                        break;
                    }

                    ApplyEventsToAggregate(filtered.Events, instance);
                }

                return instance;
            }


        }

        public void Store(IAggregate aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            var commit = new Commit
                {
                    CommitId = commitId,
                    AggregateId = aggregate.Id,
                    CommitStamp = DateTimeOffset.UtcNow
                };


            using (var session = string.IsNullOrWhiteSpace(_databaseName)
                ? _store.OpenSession()
                : _store.OpenSession(_databaseName))
            {
                session.Advanced.UseOptimisticConcurrency = true;
                session.Advanced.AllowNonAuthoritativeInformation = false;

                var stream = session.Load<EventStream>(aggregate.Id.ToStringId<EventStream>());
                if (stream == null)
                {
                    stream = new EventStream
                    {
                        Id = aggregate.Id.ToStringId<EventStream>(),
                        AggregateType = aggregate.GetType().FullName
                    };
                    session.Store(stream);
                }

                commit.CommitSequence = stream.Commits.Count + 1;
                commit.Events = aggregate.GetUncommittedEvents()
                    .Cast<object>()
                    .Select(x => new Event {Body = x, Headers = PrepareHeaders(updateHeaders)})
                    .ToList();

                stream.Version = aggregate.Version;
                stream.Snapshot = aggregate;
                stream.Commits.Add(commitId, commit);

                //pre-commit hooks
                if (_pipelineHooks.All(hook => hook.PreCommit(commit)))
                {
                    try
                    {
                        session.SaveChanges();
                        aggregate.ClearUncommittedEvents();
                    }
                    catch (Exception exception)
                    {
                        throw;
                    }

                }

            }
            //post commit hooks
            foreach (var hook in _pipelineHooks)
            {
                hook.PostCommit(commit, _databaseName);
            }
        }

        void ApplyEventsToAggregate(IEnumerable<Event> events, IAggregate aggregate)
        {
            events.Select(x => x.Body).Aggregate(aggregate, (instance, o) =>
            {
                instance.ApplyEvent(o);
                return instance;
            });
        }
        Dictionary<string, object> PrepareHeaders(Action<IDictionary<string, object>> updateHeaders)
        {
            var headers = new Dictionary<string, object>();

            if (updateHeaders != null)
                updateHeaders(headers);

            return headers;
        }
        public void Dispose()
        {
            _store = null;
            _pipelineHooks = null;

        }
    }
}