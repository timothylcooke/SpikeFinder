using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SpikeFinder.Extensions
{
    public static class EnumExtensions
    {
        public static IEnumerable<T> GetAllEnumValues<T>() where T : Enum => typeof(T).GetEnumValues().OfType<T>();


        public static IEnumerable<TAttribute> GetCustomEnumAttributes<TAttribute>(this Enum? @enum)
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

        public static Dictionary<T, string> GetEnumDescriptions<T>()
            where T : Enum
        {
            return GetAllEnumValues<T>().ToDictionary(x => x, x => x.GetCustomEnumAttributes<DescriptionAttribute>().SingleOrDefault()?.Description ?? typeof(T).GetEnumName(x)!);
        }
    }
}
