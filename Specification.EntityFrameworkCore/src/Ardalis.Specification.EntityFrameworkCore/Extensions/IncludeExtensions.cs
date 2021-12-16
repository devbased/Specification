using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Ardalis.Specification.EntityFrameworkCore
{
    public static class IncludeExtensions
    {
        internal static readonly MethodInfo IncludeMethodInfo = typeof(EntityFrameworkQueryableExtensions)
            .GetTypeInfo().GetDeclaredMethods(nameof(EntityFrameworkQueryableExtensions.Include))
            .Single(mi => mi.GetGenericArguments().Length == 2
                && mi.GetParameters().Any(pi => pi.ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)));

        internal static readonly MethodInfo ThenIncludeAfterReferenceMethodInfo
            = typeof(EntityFrameworkQueryableExtensions)
                .GetTypeInfo().GetDeclaredMethods(nameof(ThenInclude))
                .Single(
                    mi => mi.GetGenericArguments().Count() == 3
                        && mi.GetParameters()[0].ParameterType.GenericTypeArguments[1].IsGenericParameter);

        internal static readonly MethodInfo ThenIncludeAfterEnumerableMethodInfo
            = typeof(EntityFrameworkQueryableExtensions)
                .GetTypeInfo().GetDeclaredMethods(nameof(ThenInclude))
                .Where(mi => mi.GetGenericArguments().Count() == 3)
                .Single(
                    mi =>
                    {
                        var typeInfo = mi.GetParameters()[0].ParameterType.GenericTypeArguments[1];
                        return typeInfo.IsGenericType
                            && typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>);
                    });

        private static readonly CachedReadConcurrentDictionary<(Type EntityType, Type PropertyType, Type? PreviousPropertyType), Lazy<Func<IQueryable, LambdaExpression, IQueryable>>> IncludesCache =
            new CachedReadConcurrentDictionary<(Type EntityType, Type PropertyType, Type? PreviousPropertyType), Lazy<Func<IQueryable, LambdaExpression, IQueryable>>>();

        public static IQueryable<T> Include<T>(this IQueryable<T> source, IncludeExpressionInfo info)
        {
            _ = info ?? throw new ArgumentNullException(nameof(info));

            var queryExpr = Expression.Call(
                typeof(EntityFrameworkQueryableExtensions),
                "Include",
                new Type[] {
                    info.EntityType,
                    info.PropertyType
                },
                source.Expression,
                info.LambdaExpression
                );

            return source.Provider.CreateQuery<T>(queryExpr);
        }

        public static IQueryable<T> ThenInclude<T>(this IQueryable<T> source, IncludeExpressionInfo info)
        {
            _ = info ?? throw new ArgumentNullException(nameof(info));
            _ = info.PreviousPropertyType ?? throw new ArgumentNullException(nameof(info.PreviousPropertyType));

            var queryExpr = Expression.Call(
                typeof(EntityFrameworkQueryableExtensions),
                "ThenInclude",
                new Type[] {
                    info.EntityType,
                    info.PreviousPropertyType,
                    info.PropertyType
                },
                source.Expression,
                info.LambdaExpression
                );

            return source.Provider.CreateQuery<T>(queryExpr);
        }

        public static IQueryable<T> IncludeCached<T>(this IQueryable<T> source, IncludeExpressionInfo info)
        {
            _ = info ?? throw new ArgumentNullException(nameof(info));

            var include = IncludesCache.GetOrAdd((info.EntityType, info.PropertyType, null), CreateIncludeDelegate).Value;

            return (IQueryable<T>)include(source, info.LambdaExpression);
        }

        public static IQueryable<T> ThenIncludeCached<T>(this IQueryable<T> source, IncludeExpressionInfo info)
        {
            _ = info ?? throw new ArgumentNullException(nameof(info));
            _ = info.PreviousPropertyType ?? throw new ArgumentNullException(nameof(info.PreviousPropertyType));

            var thenInclude = IncludesCache.GetOrAdd((info.EntityType, info.PropertyType, info.PreviousPropertyType), CreateThenIncludeDelegate).Value;

            return (IQueryable<T>)thenInclude(source, info.LambdaExpression);
        }

        private static Lazy<Func<IQueryable, LambdaExpression, IQueryable>> CreateIncludeDelegate((Type EntityType, Type PropertyType, Type? PreviousPropertyType) cacheKey)
            => new Lazy<Func<IQueryable, LambdaExpression, IQueryable>>(() =>
            {
                var concreteInclude = IncludeMethodInfo.MakeGenericMethod(cacheKey.EntityType, cacheKey.PropertyType);
                var sourceParameter = Expression.Parameter(typeof(IQueryable));
                var selectorParameter = Expression.Parameter(typeof(LambdaExpression));

                var call = Expression.Call(
                    concreteInclude,
                    Expression.Convert(sourceParameter, typeof(IQueryable<>).MakeGenericType(cacheKey.EntityType)),
                    Expression.Convert(selectorParameter, typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(cacheKey.EntityType, cacheKey.PropertyType))));

                var lambda = Expression.Lambda<Func<IQueryable, LambdaExpression, IQueryable>>(call, sourceParameter, selectorParameter);

                var include = lambda.Compile();

                return include;
            });

        private static Lazy<Func<IQueryable, LambdaExpression, IQueryable>> CreateThenIncludeDelegate((Type EntityType, Type PropertyType, Type? PreviousPropertyType) cacheKey)
            => new Lazy<Func<IQueryable, LambdaExpression, IQueryable>>(() =>
            {
                var previousPropertyType = cacheKey.PreviousPropertyType!;

                MethodInfo thenIncludeInfo = ThenIncludeAfterReferenceMethodInfo;
                if (previousPropertyType.IsGenericType && previousPropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    thenIncludeInfo = ThenIncludeAfterEnumerableMethodInfo;
                    previousPropertyType = previousPropertyType.GenericTypeArguments[0];
                }

                var concreteThenInclude = thenIncludeInfo.MakeGenericMethod(cacheKey.EntityType, previousPropertyType, cacheKey.PropertyType);
                var sourceParameter = Expression.Parameter(typeof(IQueryable));
                var selectorParameter = Expression.Parameter(typeof(LambdaExpression));

                var call = Expression.Call(
                    concreteThenInclude,
                    Expression.Convert(sourceParameter, typeof(IIncludableQueryable<,>).MakeGenericType(cacheKey.EntityType, cacheKey.PreviousPropertyType)),
                    Expression.Convert(selectorParameter, typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(previousPropertyType, cacheKey.PropertyType))));

                var lambda = Expression.Lambda<Func<IQueryable, LambdaExpression, IQueryable>>(call, sourceParameter, selectorParameter);

                var thenInclude = lambda.Compile();

                return thenInclude;
            });
    }
}

/// <summary>
/// A thread-safe dictionary for read-heavy workloads.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
internal class CachedReadConcurrentDictionary<TKey, TValue> : IDictionary<TKey, TValue>
{
    /// <summary>
    /// The number of cache misses which are tolerated before the cache is regenerated.
    /// </summary>
    private const int CacheMissesBeforeCaching = 10;
    private readonly ConcurrentDictionary<TKey, TValue> dictionary;
    private readonly IEqualityComparer<TKey> comparer;

    /// <summary>
    /// Approximate number of reads which did not hit the cache since it was last invalidated.
    /// This is used as a heuristic that the dictionary is not being modified frequently with respect to the read volume.
    /// </summary>
    private int cacheMissReads;

    /// <summary>
    /// Cached version of <see cref="dictionary"/>.
    /// </summary>
    private Dictionary<TKey, TValue> readCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedReadConcurrentDictionary{TKey,TValue}"/> class.
    /// </summary>
    public CachedReadConcurrentDictionary()
    {
        this.dictionary = new ConcurrentDictionary<TKey, TValue>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedReadConcurrentDictionary{TKey,TValue}"/> class
    /// that contains elements copied from the specified collection.
    /// </summary>
    /// <param name="collection">
    /// The <see cref="T:IEnumerable{KeyValuePair{TKey,TValue}}"/> whose elements are copied to the new instance.
    /// </param>
    public CachedReadConcurrentDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection)
    {
        this.dictionary = new ConcurrentDictionary<TKey, TValue>(collection);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedReadConcurrentDictionary{TKey,TValue}"/> class
    /// that contains elements copied from the specified collection and uses the specified
    /// <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>.
    /// </summary>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{TKey}"/> implementation to use when comparing keys.
    /// </param>
    public CachedReadConcurrentDictionary(IEqualityComparer<TKey> comparer)
    {
        this.comparer = comparer;
        this.dictionary = new ConcurrentDictionary<TKey, TValue>(comparer);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedReadConcurrentDictionary{TKey,TValue}"/>
    /// class that contains elements copied from the specified collection and uses the specified
    /// <see cref="T:System.Collections.Generic.IEqualityComparer{TKey}"/>.
    /// </summary>
    /// <param name="collection">
    /// The <see cref="T:IEnumerable{KeyValuePair{TKey,TValue}}"/> whose elements are copied to the new instance.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{TKey}"/> implementation to use when comparing keys.
    /// </param>
    public CachedReadConcurrentDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
    {
        this.comparer = comparer;
        this.dictionary = new ConcurrentDictionary<TKey, TValue>(collection, comparer);
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => this.GetReadDictionary().GetEnumerator();

    /// <inheritdoc />
    public void Add(KeyValuePair<TKey, TValue> item)
    {
        ((IDictionary<TKey, TValue>)this.dictionary).Add(item);
        this.InvalidateCache();
    }

    /// <inheritdoc />
    public void Clear()
    {
        this.dictionary.Clear();
        this.InvalidateCache();
    }

    /// <inheritdoc />
    public bool Contains(KeyValuePair<TKey, TValue> item) => this.GetReadDictionary().Contains(item);

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        this.GetReadDictionary().CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        var result = ((IDictionary<TKey, TValue>)this.dictionary).Remove(item);
        if (result) this.InvalidateCache();
        return result;
    }

    /// <inheritdoc />
    public int Count => this.GetReadDictionary().Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public void Add(TKey key, TValue value)
    {
        ((IDictionary<TKey, TValue>)this.dictionary).Add(key, value);
        this.InvalidateCache();
    }

    /// <summary>
    /// Adds a key/value pair to the <see cref="CachedReadConcurrentDictionary{TKey,TValue}"/> if the key does not exist.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="valueFactory">The function used to generate a value for the key</param>
    /// <returns>The value for the key. This will be either the existing value for the key if the key is already in the dictionary, or the new value if the key was not in the dictionary.</returns>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        TValue value;

        if (this.GetReadDictionary().TryGetValue(key, out value))
            return value;

        value = this.dictionary.GetOrAdd(key, valueFactory);
        InvalidateCache();

        return value;
    }

    /// <summary>
    /// Attempts to add the specified key and value.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add. The value can be a null reference (Nothing
    /// in Visual Basic) for reference types.</param>
    /// <returns>true if the key/value pair was added successfully; otherwise, false.</returns>
    public bool TryAdd(TKey key, TValue value)
    {
        if (this.dictionary.TryAdd(key, value))
        {
            this.InvalidateCache();
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public bool ContainsKey(TKey key) => this.GetReadDictionary().ContainsKey(key);

    /// <inheritdoc />
    public bool Remove(TKey key)
    {
        var result = ((IDictionary<TKey, TValue>)this.dictionary).Remove(key);
        if (result) this.InvalidateCache();
        return result;
    }

    /// <inheritdoc />
    public bool TryGetValue(TKey key, out TValue value) => this.GetReadDictionary().TryGetValue(key, out value);

    /// <inheritdoc />
    public TValue this[TKey key]
    {
        get { return this.GetReadDictionary()[key]; }
        set
        {
            this.dictionary[key] = value;
            this.InvalidateCache();
        }
    }

    /// <inheritdoc />
    public ICollection<TKey> Keys => this.GetReadDictionary().Keys;

    /// <inheritdoc />
    public ICollection<TValue> Values => this.GetReadDictionary().Values;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IDictionary<TKey, TValue> GetReadDictionary() => this.readCache ?? this.GetWithoutCache();

    private IDictionary<TKey, TValue> GetWithoutCache()
    {
        // If the dictionary was recently modified or the cache is being recomputed, return the dictionary directly.
        if (Interlocked.Increment(ref this.cacheMissReads) < CacheMissesBeforeCaching) return this.dictionary;

        // Recompute the cache if too many cache misses have occurred.
        this.cacheMissReads = 0;
        return this.readCache = new Dictionary<TKey, TValue>(this.dictionary, this.comparer);
    }

    private void InvalidateCache()
    {
        this.cacheMissReads = 0;
        this.readCache = null;
    }
}