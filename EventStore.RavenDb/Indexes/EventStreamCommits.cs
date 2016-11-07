using System.Linq;
using Raven.Client.Indexes;

namespace EventStore.RavenDb.Indexes
{
    internal class EventStreamCommits : AbstractIndexCreationTask<EventStream, Commit>
    {
        public EventStreamCommits()
        {
            Map = streams => from s in streams
                             from c in s.Commits
                             let x = c.Value
                             select new
                                 {
                                     x.CommitId,
                                     x.AggregateId,
                                     x.CommitSequence,
                                     x.CommitStamp,
                                     x.Items,
                                     x.IsDispatched,
                                     x.Events
                                 };

            Reduce = results => from r in results
                                group r by new { r.CommitId, r.AggregateId } into g
                                select new
                                    {
                                        CommitId = g.Key.CommitId,
                                        AggregateId = g.Key.AggregateId,
                                        CommitSequence = g.Select(x => x.CommitSequence).FirstOrDefault(x => x != null),
                                        CommitStamp = g.Select(x => x.CommitStamp).FirstOrDefault(x => x != null),
                                        Items = g.Select(x => x.Items).FirstOrDefault(x => x != null),
                                        IsDispatched = g.Select(x => x.IsDispatched).FirstOrDefault(x => x != null),
                                        Events = g.Select(x => x.Events).FirstOrDefault(x => x != null)
                                    };

        }
    }
}
