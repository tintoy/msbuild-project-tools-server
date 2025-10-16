using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Helper methods for working with LINQ.
    /// </summary>
    public static class LinqHelper
    {
        /// <summary>
        ///     Determines whether a sequence contains any elements.
        /// </summary>
        /// <param name="source">
        ///     The <see cref="IEnumerable"/> to check for emptiness.
        /// </param>
        /// <returns>
        ///     <c>true</c> if the source sequence contains any elements; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool Any(this IEnumerable source)
        {
            return
                TryGetNonEnumeratedCount(source, out int count) ? count != 0 :
                WithEnumerator(source);

            static bool WithEnumerator(IEnumerable source)
            {
                var e = source.GetEnumerator();
                if (e is IDisposable disp)
                    using (disp)
                        return e.MoveNext();
                else
                    return e.MoveNext();
            }
        }

        /// <summary>
        ///     Attempts to determine the number of elements in a sequence without forcing an enumeration.
        /// </summary>
        /// <param name="source">
        ///     A sequence that contains elements to be counted.
        /// </param>
        /// <param name="count">
        ///     When this method returns, contains the count of <paramref name="source" /> if successful,
        ///     or zero if the method failed to determine the count.</param>
        /// <returns>
        ///     <see langword="true" /> if the count of <paramref name="source"/> can be determined without enumeration;
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <remarks>
        ///     The method performs a series of type tests, identifying common subtypes whose
        ///     count can be determined without enumerating; this includes <see cref="ICollection{T}"/>,
        ///     <see cref="ICollection"/> as well as internal types used in the LINQ implementation.
        ///
        ///     The method is typically a constant-time operation, but ultimately this depends on the complexity
        ///     characteristics of the underlying collection implementation.
        /// </remarks>
        public static bool TryGetNonEnumeratedCount(this IEnumerable source, out int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (source is ICollection collection)
            {
                count = collection.Count;
                return true;
            }

            if (source.Cast<object>().TryGetNonEnumeratedCount(out count))
                return true;

            count = 0;
            return false;
        }
        
        /// <summary>
        ///     Flatten the sequence, enumerating nested sequences.
        /// </summary>
        /// <typeparam name="TSource">
        ///     The source element type.
        /// </typeparam>
        /// <param name="source">
        ///     The source sequence of sequences.
        /// </param>
        /// <returns>
        ///     The flattened sequence.
        /// </returns>
        public static IEnumerable<TSource> Flatten<TSource>(this IEnumerable<IEnumerable<TSource>> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source.SelectMany(items => items);
        }
        
        /// <summary>
        ///     Creates a sequence with <paramref name="source"/> as the only one element.
        /// </summary>
        /// <typeparam name="T">
        ///     Type of the source.
        /// </typeparam>
        /// <param name="source"></param>
        /// <returns>
        ///     An <see cref="IEnumerable{T}"/> that contains the source as the only one element.
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IEnumerable<T> Yield<T>(this T source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return YieldEnumerator(source);

            static IEnumerable<T> YieldEnumerator(T source)
            {
                yield return source;
            }
        }
    }
}
