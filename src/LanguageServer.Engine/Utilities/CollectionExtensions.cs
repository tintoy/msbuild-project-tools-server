using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Extension methods for working with collections.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Deconstruct a key / value pair.
        /// </summary>
        /// <typeparam name="TKey">
        ///     The key type.
        /// </typeparam>
        /// <typeparam name="TValue">
        ///     The value type.
        /// </typeparam>
        /// <param name="keyValuePair">
        ///     The <see cref="KeyValuePair{TKey, TValue}"/> to deconstruct.
        /// </param>
        /// <param name="key">
        ///     Receives the key.
        /// </param>
        /// <param name="value">
        ///     Receives the value.
        /// </param>
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> keyValuePair, out TKey key, out TValue value)
        {
            key = keyValuePair.Key;
            value = keyValuePair.Value;
        }

        /// <summary>
        ///     Add multiple key / value pairs to a dictionary.
        /// </summary>
        /// <typeparam name="TKey">
        ///     The dictionary key type.
        /// </typeparam>
        /// <typeparam name="TValue">
        ///     The dictionary value type.
        /// </typeparam>
        /// <param name="dictionary">
        ///     The <see cref="IDictionary{TKey, TValue}"/> to update.
        /// </param>
        /// <param name="keyValuePairs">
        ///     A sequence of <see cref="KeyValuePair{TKey, TValue}"/>s to add to the <paramref name="dictionary"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     Either <paramref name="dictionary"/> or <paramref name="keyValuePairs"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     One of the <paramref name="keyValuePairs"/> has a key that is already present in the dictionary.
        /// </exception>
        public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            ArgumentNullException.ThrowIfNull(dictionary);

            ArgumentNullException.ThrowIfNull(keyValuePairs);

            foreach (KeyValuePair<TKey, TValue> keyValuePair in keyValuePairs)
                dictionary.Add(keyValuePair.Key, keyValuePair.Value);
        }

        /// <summary>
        ///     Set multiple key / value pairs in a dictionary.
        /// </summary>
        /// <typeparam name="TKey">
        ///     The dictionary key type.
        /// </typeparam>
        /// <typeparam name="TValue">
        ///     The dictionary value type.
        /// </typeparam>
        /// <param name="dictionary">
        ///     The <see cref="IDictionary{TKey, TValue}"/> to update.
        /// </param>
        /// <param name="keyValuePairs">
        ///     A sequence of <see cref="KeyValuePair{TKey, TValue}"/>s to set in the <paramref name="dictionary"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     Either <paramref name="dictionary"/> or <paramref name="keyValuePairs"/> is <c>null</c>.
        /// </exception>
        public static void SetRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            ArgumentNullException.ThrowIfNull(dictionary);

            ArgumentNullException.ThrowIfNull(keyValuePairs);

            foreach (KeyValuePair<TKey, TValue> keyValuePair in keyValuePairs)
                dictionary[keyValuePair.Key] = keyValuePair.Value;
        }
    }
}
