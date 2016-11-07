using EventStore.RavenDb.Infrastructure;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Json.Linq;
using System;
using System.Transactions;

namespace EventStore.RavenDb.Dispatchers
{
    internal class SynchronousDispatchScheduler : IScheduleDispatches
    {
        readonly IDispatchCommits _dispatcher;
        readonly IDocumentStore _store;


        public SynchronousDispatchScheduler(IDispatchCommits dispatcher, IDocumentStore store)
        {
            _dispatcher = dispatcher;
            _store = store;


        }


        public void Dispose()
        {
            _dispatcher.Dispose();
            GC.SuppressFinalize(this);
        }

        public virtual void ScheduleDispatch(Commit commit, string databaseName)
        {
            _dispatcher.Dispatch(commit);
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                //using (var t = new TransactionScope(TransactionScopeOption.Suppress))
                //{
                    _store.DatabaseCommands.Patch(commit.AggregateId.ToStringId<EventStream>(),
                                                  new ScriptedPatchRequest
                                                      {
                                                          Script = "this.Commits[CommitId].IsDispatched=true",
                                                          Values = { { "CommitId", RavenJValue.FromObject(commit.CommitId.ToString()) } }
                                                      });
                    //t.Complete();
                //}
            }
            else
            {
                //using (var t = new TransactionScope(TransactionScopeOption.Suppress))
                //{
                    var db = _store.DatabaseCommands.ForDatabase(databaseName);
                    db.Patch(commit.AggregateId.ToStringId<EventStream>(),
                             new ScriptedPatchRequest
                                 {
                                     Script = "this.Commits[CommitId].IsDispatched=true",
                                     Values = { { "CommitId", RavenJValue.FromObject(commit.CommitId.ToString()) } }
                                 });

                    //t.Complete();
                //}
            }
        }
    }
}