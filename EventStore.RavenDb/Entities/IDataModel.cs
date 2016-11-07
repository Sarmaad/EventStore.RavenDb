using System;

namespace EventStore.RavenDb.Entities
{
    public interface IDataModel
    {
        Guid Id { get; set; }
        string IdString { get; set; }
    }
}
