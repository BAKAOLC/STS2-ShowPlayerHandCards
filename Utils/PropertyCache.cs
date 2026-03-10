using System.Reflection;

namespace STS2ShowPlayerHandCards.Utils
{
    /// <summary>
    ///     Non-generic helper class to cache reflection information per type.
    ///     Avoids static fields in generic types while maintaining performance.
    /// </summary>
    internal static class PropertyCache
    {
        private static readonly Dictionary<Type, PropertyInfo[]> Cache = new();
        private static readonly Lock Lock = new();

        public static PropertyInfo[] GetProperties(Type type)
        {
            lock (Lock)
            {
                if (Cache.TryGetValue(type, out var cached))
                    return cached;

                var properties = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p is { CanRead: true, CanWrite: true })
                    .ToArray();

                Cache[type] = properties;
                return properties;
            }
        }
    }
}
