using CommonDomain;
using EventStore.RavenDb.Conversion;
using EventStore.RavenDb.Dispatchers;
using EventStore.RavenDb.Indexes;
using EventStore.RavenDb.Infrastructure;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace EventStore.RavenDb
{
    public sealed class EventSource
    {
        public static Func<string> DatabaseName = () => "";
        readonly IDocumentStore _store;
        readonly List<IPipelineHook> _pipelineHooks;
        bool _isInitialized;
        bool _hasDispatcher;
        readonly ConcurrentBag<string> _initializedDatabases = new ConcurrentBag<string>();

        static readonly object InitLock = new object();


        public EventSource(IDocumentStore store)
        {
            if (store == null) throw new ArgumentNullException("store");

            _store = store;
            _pipelineHooks = new List<IPipelineHook>();
            _isInitialized = false;
        }

        public EventSource RegisterHook(IPipelineHook pipelineHook)
        {
            _pipelineHooks.Add(pipelineHook);
            return this;
        }
        public EventSource UsingSynchronousDispatchScheduler(IDispatchCommits instance)
        {
            var dispatcher = new SynchronousDispatchScheduler(instance, _store);
            _pipelineHooks.Add(new DispatchSchedulerPipelineHook(dispatcher));
            _hasDispatcher = true;
            return this;
        }
        public EventSource UsingAsynchronousDispatchScheduler(IDispatchCommits instance)
        {
            var dispatcher = new AsynchronousDispatchScheduler(instance, _store);
            _pipelineHooks.Add(new DispatchSchedulerPipelineHook(dispatcher));
            _hasDispatcher = true;
            return this;
        }
        public EventSource WithConvertersFrom(params Assembly[] assemblies)
        {
            var converters = GetConverters(assemblies);
            _pipelineHooks.Add(new EventUpconverterPipelineHook(converters));
            return this;
        }
        public EventSource Initialize()
        {
            var databaseName = ((DocumentStore)_store).DefaultDatabase;
            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                //create the index at that datbase.. also override the function
                new EventStreamCommits().Execute(_store);

                DatabaseName = () => databaseName;

                //TODO: register database with commit monitoring service
            }
            _isInitialized = true;
            return this;
        }

        public IEventStoreSession OpenSession(string database = null)
        {
            if (!_isInitialized)
                throw new Exception("Initialize Event Source first!");

            var databaseName = string.IsNullOrWhiteSpace(database) ? DatabaseName.Invoke() : database;
            PrepareDatabase(databaseName);

            return new EventStoreSession(_store, _pipelineHooks, databaseName);
        }

        public void ReplayEvents(IDispatchCommits dispatcher)
        {
            if (!_isInitialized)
                throw new Exception("Initialize Event Source first!");

            var databaseName = DatabaseName.Invoke();
            PrepareDatabase(databaseName);

            // wait for indexing to complete
            var indexingTask = Task.Factory.StartNew(
                () =>
                {
                    while (true)
                    {
                        var s = string.IsNullOrWhiteSpace(databaseName)
                                    ? _store.DatabaseCommands.GetStatistics().StaleIndexes
                                    : _store.DatabaseCommands.ForDatabase(databaseName).GetStatistics().StaleIndexes;
                        if (!s.Contains("EventStreamCommits")) break;
                        Thread.Sleep(2000);
                    }
                });
            indexingTask.Wait(120000);

            var current = 0;
            while (true)
            {
                using (var session = string.IsNullOrWhiteSpace(databaseName) ? _store.OpenSession() : _store.OpenSession(databaseName))
                {
                    var commits = session.Query<Commit, EventStreamCommits>()
                                         .Customize(x => x.WaitForNonStaleResultsAsOfNow())
                                         .OrderBy(x => x.CommitStamp)
                                         .Skip(current)
                                         .Take(1024)
                                         .ToList();

                    if (commits.Count == 0) break;

                    commits = FilteredCommits(commits);
                    commits.ForEach(dispatcher.Dispatch);

                    current += commits.Count;
                }
            }
        }
        public void StoreCommits(Type aggregateType, Guid aggregateId, IEnumerable<Commit> commits, bool continueOnError = false)
        {
            if (!_isInitialized)
                throw new Exception("Initialize Event Source first!");

            var databaseName = DatabaseName.Invoke();
            if (!string.IsNullOrWhiteSpace(databaseName))
                _store.DatabaseCommands.EnsureDatabaseExists(databaseName);

            using (var t = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                using (var session = string.IsNullOrWhiteSpace(databaseName) ? _store.OpenSession() : _store.OpenSession(databaseName))
                {
                    session.Advanced.UseOptimisticConcurrency = true;
                    session.Advanced.AllowNonAuthoritativeInformation = false;

                    var id = aggregateId.ToStringId<EventStream>();
                    var stream = session.Load<EventStream>(id);
                    if (stream == null)
                    {
                        stream = new EventStream { Id = id, AggregateType = aggregateType.FullName };
                        session.Store(stream);
                    }

                    commits = FilteredCommits(commits.OrderBy(x => x.CommitStamp));
                    commits.ToList().ForEach(commit =>
                        {
                            commit.CommitSequence = stream.Commits.Count + 1;
                            stream.Commits.Add(commit.CommitId, commit);
                        });

                    var instance = (IAggregate)Activator.CreateInstance(aggregateType);
                    var exceptions = ApplyEventsToAggregate(stream.Commits.Select(x => x.Value), instance, continueOnError);

                    if (exceptions.Any())
                    {
                        var errorId = aggregateId.ToStringId<ErrorStream>();
                        var commitError = session.Load<ErrorStream>(errorId);
                        if (commitError == null)
                        {
                            commitError = new ErrorStream { Id = errorId, AggregateType = stream.AggregateType };
                            session.Store(commitError);
                        }
                        foreach (var e in exceptions)
                        {
                            var commit = stream.Commits[e.Key];
                            commitError.Commits.Add(new ErrorCommit { Commit = commit, Error = e.Value });
                            stream.Commits.Remove(e.Key);
                        }
                    }

                    stream.Version = instance.Version;
                    stream.Snapshot = instance;

                    session.SaveChanges();
                }
                t.Complete();
            }
        }
        public void UpdateSnapshots(Func<string, Type> aggregateTypeResolver, bool continueOnError)
        {
            if (!_isInitialized)
                throw new Exception("Initialize Event Source first!");

            var databaseName = DatabaseName.Invoke();
            PrepareDatabase(databaseName);

            using (var largeSession = string.IsNullOrWhiteSpace(databaseName) ? _store.OpenSession() : _store.OpenSession(databaseName))
            {
                var query = largeSession.Advanced.LuceneQuery<Raven.Json.Linq.RavenJObject>("Raven/DocumentsByEntityName").WhereStartsWith("Tag", "EventStreams");
                var ret = largeSession.Advanced.Stream(query);

                while (ret.MoveNext())
                {
                    using (var t = new TransactionScope(TransactionScopeOption.RequiresNew))
                    {
                        using (var session = string.IsNullOrWhiteSpace(databaseName) ? _store.OpenSession() : _store.OpenSession(databaseName))
                        {
                            session.Advanced.AllowNonAuthoritativeInformation = false;

                            var stream = session.Load<EventStream>(ret.Current.Key);

                            var commits = FilteredCommits(stream.Commits.Select(x => x.Value));

                            var aggregateType = aggregateTypeResolver.Invoke(stream.AggregateType);
                            var instance = (IAggregate)Activator.CreateInstance(aggregateType);

                            var exceptions = ApplyEventsToAggregate(commits, instance, continueOnError);
                            if (exceptions.Any())
                            {
                                var id = stream.Id.ToGuidId().ToStringId<ErrorStream>();
                                var commitError = session.Load<ErrorStream>(id);
                                if (commitError == null)
                                {
                                    commitError = new ErrorStream
                                        {
                                            Id = id,
                                            AggregateType = stream.AggregateType
                                        };

                                    session.Store(commitError);
                                }
                                foreach (var e in exceptions)
                                {
                                    var commit = stream.Commits[e.Key];
                                    commitError.Commits.Add(new ErrorCommit { Commit = commit, Error = e.Value });
                                    stream.Commits.Remove(e.Key);
                                }
                            }

                            stream.Version = instance.Version;
                            stream.Snapshot = instance;

                            session.SaveChanges();
                        }
                        t.Complete();
                    }
                }
            }
        }

        void PrepareDatabase(string databaseName)
        {
            if (!_initializedDatabases.Contains(databaseName) && !string.IsNullOrWhiteSpace(databaseName))
                lock (InitLock)
                {
                    if (!_initializedDatabases.Contains(databaseName))
                    {
                        _store.DatabaseCommands.EnsureDatabaseExists(databaseName);
                        new EventStreamCommits().Execute(_store.DatabaseCommands.ForDatabase(databaseName), _store.Conventions);

                        //TODO: register database with commit monitoring service

                        _initializedDatabases.Add(databaseName);
                    }
                }
        }
        Dictionary<Guid, Exception> ApplyEventsToAggregate(IEnumerable<Commit> commits, IAggregate aggregate, bool continueOnError)
        {
            var exceptions = new Dictionary<Guid, Exception>();
            commits.Aggregate(aggregate, (instance, o) =>
            {
                try
                {
                    o.Events.ForEach(e => instance.ApplyEvent(e.Body));
                }
                catch (Exception exception)
                {
                    if (!continueOnError)
                        throw;

                    exceptions.Add(o.CommitId, exception);
                }
                return instance;
            });

            return exceptions;
        }
        static IDictionary<Type, Func<object, object>> GetConverters(IEnumerable<Assembly> toScan)
        {
            var c = from a in toScan
                    from t in a.GetTypes()
                    let i = t.GetInterface(typeof(IUpconvertEvents<,>).FullName)
                    where i != null
                    let sourceType = i.GetGenericArguments().First()
                    let convertMethod = i.GetMethods(BindingFlags.Public | BindingFlags.Instance).First()
                    let instance = Activator.CreateInstance(t)
                    select new KeyValuePair<Type, Func<object, object>>(
                        sourceType, e => convertMethod.Invoke(instance, new[] { e }));
            try
            {
                return c.ToDictionary(x => x.Key, x => x.Value);
            }
            catch (ArgumentException e)
            {
                throw new MultipleConvertersFoundException(e.Message, e);
            }
        }
        List<Commit> FilteredCommits(IEnumerable<Commit> commits)
        {
            var selectedCommits = new List<Commit>();

            foreach (var commit in commits)
            {
                var filtered = commit;
                foreach (var hook in _pipelineHooks.Where(x => (filtered = x.Select(filtered)) == null))
                {
                    throw new Exception("a pipeline hook returned null. {0}".FormatWith(hook.GetType().FullName));
                }
                selectedCommits.Add(filtered);
            }

            return selectedCommits;
        }
    }
}
