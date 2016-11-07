using System;
using System.Collections.Generic;

namespace EventStore.RavenDb
{
    public class ErrorStream
    {
        public string Id { get; set; }
        public string AggregateType { get; set; }
        public int Items { get { return Commits.Count; } }
        public List<ErrorCommit> Commits { get; set; }

        public ErrorStream()
        {
            Commits = new List<ErrorCommit>();
        }

    }

    public class ErrorCommit
    {
        public Commit Commit { get; set; }
        public Exception Error { get; set; }
    }
}