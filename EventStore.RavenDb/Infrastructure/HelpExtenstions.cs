using System;

namespace EventStore.RavenDb.Infrastructure
{
    internal static class HelpExtenstions
    {
        public static string ToStringId<T>(this Guid value) where T : class
        {
            return Raven.Client.Util.Inflector.Pluralize(typeof(T).Name) + "/" + value.ToString();
        }

        public static Guid ToGuidId(this string value)
        {
            var split = value.Split('/');
            if (split.Length <= 0) return Guid.Empty;

            Guid gValue;

            return Guid.TryParse(split[1], out gValue) ? gValue : Guid.Empty;
        }

        public static string FormatWith(this string value, params object[] values)
        {
            return string.Format(value, values);
        }
    }


}
