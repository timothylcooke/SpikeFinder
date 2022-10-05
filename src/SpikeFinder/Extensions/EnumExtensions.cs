using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikeFinder.Extensions
{
    public static class EnumExtensions
    {
        public static IEnumerable<T> GetAllEnumValues<T>() where T : Enum => typeof(T).GetEnumValues().OfType<T>();


        public static IEnumerable<TAttribute> GetCustomEnumAttributes<TAttribute>(this object? @enum)
            where TAttribute : Attribute
        {
            var type = @enum?.GetType();
            if (type?.IsEnum == true)
            {
                return type.GetField(type.GetEnumName(@enum!)!)!
                    .GetCustomAttributes(typeof(TAttribute), false)
                    .OfType<TAttribute>();
            }
            else
            {
                throw new ArgumentException("You must pass an enum to this method.", nameof(@enum));
            }
        }
    }
}
