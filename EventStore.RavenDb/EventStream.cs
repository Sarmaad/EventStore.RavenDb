using System;
using System.Collections.Generic;

namespace EventStore.RavenDb
{
    public class EventStream
    {
        public string Id { get; set; }
        public string AggregateType { get; set; }
        public int Version { get; set; }
        public Dictionary<Guid, Commit> Commits { get; set; }

        public object Snapshot { get; set; }

        public EventStream()
        {
            Commits = new Dictionary<Guid, Commit>();
        }
    }

    public class Commit
    {
        public Guid CommitId { get; set; }
        public Guid AggregateId { get; set; }
        public int CommitSequence { get; set; }
        public DateTimeOffset CommitStamp { get; set; }
        public int Items { get { return Events.Count; } }
        public bool IsDispatched { get; set; }
        public List<Event> Events { get; set; }

        public Commit()
        {
            Events = new List<Event>();
        }
    }

    public class Event
    {

        public Dictionary<string, object> Headers { get; set; }
        public object Body { get; set; }

        public Event()
        {
            Headers = new Dictionary<string, object>();
        }
    }
}
