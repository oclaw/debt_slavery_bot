using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;


namespace DebtSlaveryBot.Helpers
{
    public static class Extensions
    {
        public static DbContext DeleteAll<T>(this DbContext ctx)
            where T: class
        {
            foreach (var record in ctx.Set<T>())
            {
                ctx.Entry(record).State = EntityState.Deleted;
            }
            return ctx;
        }

        public static bool IsOneOf<T>(this T value, params T[] values) => values.Contains(value);

        public static bool ContainsAll<T>(this IEnumerable<T> set, params T[] values)
        {
            foreach (var value in values)
                if (!set.Contains(value))
                    return false;
            return true;
        }

        public static List<PropertyInfo> GetDbSetProperties(this DbContext context)
        {
            var dbSetProperties = new List<PropertyInfo>();
            var properties = context.GetType().GetProperties();

            foreach (var property in properties)
            {
                var setType = property.PropertyType;

                var isDbSet = setType.IsGenericType && (typeof(DbSet<>).IsAssignableFrom(setType.GetGenericTypeDefinition()));

                if (isDbSet)
                {
                    dbSetProperties.Add(property);
                }
            }
            return dbSetProperties;
        }

    //    var dbSets = this.GetDbSetProperties();
    //    var thisType = GetType();
    //    var entityMethod = thisType.GetMethod("Entity");

    //        foreach (var type in dbSets)
    //        {
    //            var subProps = type.PropertyType.GetProperties();
    //            foreach (var subProp in subProps)
    //            {
    //                var attrs = subProp.GetCustomAttributes(false);
    //                if (attrs.Any(attr => attr is UniqueKeyAttribute))
    //                {
    //                    var concreteEntityMethod = entityMethod.MakeGenericMethod(type.PropertyType);
    //    concreteEntityMethod.Invoke(this)
    //                    modelBuilder.Entity<>
    //                }
    //}
    //System.Console.WriteLine($"Type {type.Name}");
    //        }


    }
}
