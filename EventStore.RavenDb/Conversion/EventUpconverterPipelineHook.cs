using System;
using System.Collections.Generic;

namespace EventStore.RavenDb.Conversion
{
    public class EventUpconverterPipelineHook : IPipelineHook
    {
        private readonly IDictionary<Type, Func<object, object>> converters;

        public EventUpconverterPipelineHook(IDictionary<Type, Func<object, object>> converters)
        {
            if (converters == null)
                throw new ArgumentNullException("converters");

            this.converters = converters;
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            this.converters.Clear();
        }

        public virtual Commit Select(Commit committed)
        {
            foreach (var eventMessage in committed.Events)
                eventMessage.Body = this.Convert(eventMessage.Body);

            return committed;
        }
        private object Convert(object source)
        {
            Func<object, object> converter;
            if (!this.converters.TryGetValue(source.GetType(), out converter))
                return source;

            var target = converter(source);

            return this.Convert(target);
        }

        public virtual bool PreCommit(Commit attempt)
        {
            return true;
        }
        public virtual void PostCommit(Commit committed,string databaseName)
        {
        }
    }
}
