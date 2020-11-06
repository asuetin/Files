﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Files.Common
{
    public static class Extensions
    {
        public static IEnumerable<TSource> ExceptBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            IEnumerable<TSource> other,
            Func<TSource, TKey> keySelector)
        {
            var set = new HashSet<TKey>(other.Select(keySelector));
            foreach (var item in source)
                if (set.Add(keySelector(item)))
                    yield return item;
        }

        public static IEnumerable<TSource> IntersectBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            IEnumerable<TSource> other,
            Func<TSource, TKey> keySelector)
        {
            return source.Join(other.Select(keySelector), keySelector, id => id, (o, id) => o);
        }

        public static TOut Get<TOut, TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TOut defaultValue = default(TOut))
        {
            // If setting doesn't exist, create it.
            if (!dictionary.ContainsKey(key))
                dictionary[key] = (TValue)(object)defaultValue;

            return (TOut)(object)dictionary[key];
        }

        public static DateTime ToDateTime(this System.Runtime.InteropServices.ComTypes.FILETIME time)
        {
            ulong high = (ulong)time.dwHighDateTime;
            uint low = (uint)time.dwLowDateTime;
            long fileTime = (long)((high << 32) + low);
            try
            {
                return DateTime.FromFileTimeUtc(fileTime);
            }
            catch
            {
                return DateTime.FromFileTimeUtc(0xFFFFFFFF);
            }
        }

        public static async Task WithTimeout(this Task task,
            TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            {
                await task;
            }
        }

        public static async Task<T> WithTimeout<T>(this Task<T> task,
            TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            {
                return await task;
            }
            return default(T);
        }
    }
}