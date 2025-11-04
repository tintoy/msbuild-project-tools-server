using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    /// <summary>
    ///     Extensions to the functionality of <see cref="TextParser{T}"/>.
    /// </summary>
    static class TextParserExtensions
    {
        private static class IdentityFunction<TElement>
        {
            public static Func<TElement, TElement> Instance => x => x;

            public class FromDerivedType<TDerived>
                where TDerived : TElement
            {
                public static Func<TDerived, TElement> Instance => x => x;
            }
        }

        private sealed class ReferenceEqualityComparer<T> : EqualityComparer<T> where T : class
        {
            public override bool Equals(T x, T y) => ReferenceEquals(x, y);

            public override int GetHashCode(T obj) => obj?.GetHashCode() ?? 0;
        }

        private sealed class DistinctItemComparer<T>
        {
            public static EqualityComparer<T> Default { get; } = CreateDefault();

            private static EqualityComparer<T> CreateDefault()
            {
                if (typeof(T).IsClass)
                {
                    var comparerType = typeof(ReferenceEqualityComparer<>).MakeGenericType(typeof(T));
                    return (EqualityComparer<T>)Activator.CreateInstance(comparerType);
                }
                else
                {
                    return EqualityComparer<T>.Default;
                }
            }
        }

        public static class Tags
        {
            public struct LHS<T, U> where T : U { }
            public struct RHS<U, T> where T : U { }
        }

        /// <summary>
        ///     Specifies that a parse result is not to contain duplicates,
        ///     as specified by the provided equality comparer.
        /// </summary>
        /// <typeparam name="TItem">
        ///     The type of the items in the collection produced by
        ///     <paramref name="parser"/>.
        /// </typeparam>
        /// <param name="parser">
        ///     A parser.
        /// </param>
        /// <param name="comparer">
        ///     A comparer for items.
        /// </param>
        /// <returns>
        ///     A parser that fails on duplicates.
        /// </returns>
        public static TextParser<TItem[]> WithoutDuplicates<TItem>(
            this TextParser<TItem[]> parser,
            IEqualityComparer<TItem> comparer = null) => WithoutDuplicatesBy(parser, IdentityFunction<TItem>.Instance, comparer);

        /// <summary>
        ///     Specifies that a parse result is not to contain duplicates,
        ///     as specified by the provided key selector and equality comparer.
        /// </summary>
        /// <typeparam name="TItem">
        ///     The type of the items in the collection produced by
        ///     <paramref name="parser"/>.</typeparam>
        /// <typeparam name="TKey">
        ///     The type of the key by which elements of the collection produced
        ///     by <paramref name="parser"/> will be distinguished.
        /// </typeparam>
        /// <param name="parser">
        ///     A parser.
        /// </param>
        /// <param name="keySelector">
        ///     A function to extract the key for each element.
        /// </param>
        /// <param name="comparer">
        ///     An optional comparer for items.
        /// </param>
        /// <returns>
        ///     A parser that fails on duplicates.
        /// </returns>
        public static TextParser<TItem[]> WithoutDuplicatesBy<TItem, TKey>(
            this TextParser<TItem[]> parser,
            Func<TItem, TKey> keySelector,
            IEqualityComparer<TKey> comparer = null) => input =>
            {
                var result = parser(input);
                if (!result.HasValue)
                {
                    return result;
                }

                var resultValue = result.Value;
                var itemType = typeof(TItem);
                //HINT: use shortcut iteration if item type is primitive or reference type
                if (!itemType.IsValueType || itemType.IsEnum || itemType.IsPrimitive)
                {
                    var itemComparer = DistinctItemComparer<TItem>.Default;
                    var itemsAreDistinct = resultValue.Zip(resultValue.DistinctBy(keySelector, comparer), itemComparer.Equals);
                    return itemsAreDistinct.All(IdentityFunction<bool>.Instance)
                        ? result
                        : Result.Empty<TItem[]>(input, "Duplicates detected");
                }
                //HINT: else if it is complex value type then iterate through all distinct items and compare counts
                else
                {
                    var originalCount = resultValue.Length;
                    var distinctCount = resultValue.DistinctBy(keySelector, comparer).Count();
                    return originalCount == distinctCount
                        ? result
                        : Result.Empty<TItem[]>(input, "Duplicates detected");
                }
            };

        /// <summary>
        ///     Constructs a parser that combines the results of two consecutive
        ///     parsers.</summary>
        /// <typeparam name="TFirst">
        ///     The result type of the first parser.
        /// </typeparam>
        /// <typeparam name="TSecond">
        ///     The result type of the second parser.
        /// </typeparam>
        /// <typeparam name="TResult">
        ///     The result type of the result parser.
        /// </typeparam>
        /// <param name="first">
        ///     The parser to apply first.
        /// </param>
        /// <param name="second">
        ///     The parser to apply second.
        /// </param>
        /// <param name="combiner">
        ///     A function which combines the results of the provided parsers.
        /// </param>
        /// <returns>
        ///     A parser which is the combination of the provided parsers.
        /// </returns>
        public static TextParser<TResult> Apply<TFirst, TSecond, TResult>(
            this TextParser<TFirst> first,
            TextParser<TSecond> second,
            Func<TFirst, TSecond, TResult> combiner) => input =>
            {
                switch (first.Invoke(input))
                {
                    case var r1 when r1.HasValue && r1.Value is var v1 && r1.Remainder is var rm1:
                        switch (second(rm1))
                        {
                            case var r2 when r2.HasValue && r2.Value is var v2 && r2.Remainder is var rm2:
                                return Result.Value(combiner(v1, v2), input, rm2);
                            case var r2: return Result.CastEmpty<TSecond, TResult>(r2);
                        }
                        ;
                    case var r1: return Result.CastEmpty<TFirst, TResult>(r1);
                }
                ;
            };

        /// <summary>
        ///     Wrapper type to help in partial type inference for generic extension methods.
        /// </summary>
        /// <remarks>
        ///     Partial type inference on generic methods is still not possible with C#, see:
        ///     https://stackoverflow.com/questions/2893698/partial-generic-type-inference-possible-in-c
        ///     https://github.com/dotnet/csharplang/issues/1349
        /// </remarks>
        public struct CastHelper<T>
        {
            static readonly ConcurrentDictionary<Type, Delegate> _castMethods =
                new ConcurrentDictionary<Type, Delegate>();
            readonly TextParser<T> _parser;
            /// <summary>
            /// 
            /// </summary>
            /// <param name="parser">
            ///     The parser.
            /// </param>
            public CastHelper(TextParser<T> parser)
            {
                _parser = parser;
            }

            /// <summary>
            ///     Cast the parser result type.
            /// </summary>
            /// <typeparam name="TResult">
            ///     The parser result type.
            /// </typeparam>
            /// <returns>
            ///     The parser, as one for a sub-type of <typeparamref name="TResult"/>.
            /// </returns>
            /// <remarks>
            ///     Generic constraint to define <typeparamref name="T"/>
            ///     as sub-type of <typeparamref name="TResult"/> is not possible
            ///     for this method, which does not help in compile-time errors.
            ///     There is only a runtime check for that.
            /// </remarks>
            public TextParser<TResult> As<TResult>()
            {
                if (!typeof(TResult).IsAssignableFrom(typeof(T)))
                    throw new InvalidCastException($"Types {typeof(T).FullName} and {typeof(TResult).FullName} are not compatible.");

                // When type parameter T of Result<T> is a value type, then it
                // is not binary compatible to other Result<TResult> and casting
                // to interface would also need boxing, so fall back to the
                // default extension method.
                if (typeof(T).IsValueType)
                {
                    var castmethod = _castMethods.GetOrAdd(typeof(TResult), _ =>
                    {
                        var mi = (from m in typeof(Combinators).GetMethods()
                                  where m.Name == nameof(Combinators.Cast) &&
                                    m.GetGenericArguments().Length == 2
                                  select m).FirstOrDefault();
                        return mi.MakeGenericMethod(typeof(T), typeof(TResult))
                            .CreateDelegate<Func<TextParser<T>, TextParser<TResult>>>();
                    }) as Func<TextParser<T>, TextParser<TResult>>;
                    return castmethod(_parser);
                }

                // Otherwise when type parameter is reference type and this
                // is also compatible to TResult, then Result<T> is binary
                // compatible, which means it can safely be cast by
                // reinterpretation of the object in memory.
                // And so can it be done for the delegate which returns
                // these Result<T>.
                var parser = _parser;
                return Unsafe.As<TextParser<T>, TextParser<TResult>>(ref parser);
            }
        }

        /// <summary>
        ///     Convenience extension method to help in partial type inference to 
        ///     construct a parser that takes the result of <paramref name="parser"/>
        ///     and casts it to the type of type parameter of
        ///     <see cref="CastHelper{T}.As{TResult}"/>.
        /// </summary>
        /// <typeparam name="T">
        ///     The type of value being parsed.
        /// </typeparam>
        /// <param name="parser">
        ///     The parser.
        /// </param>
        /// <returns>
        ///     Wrapper type to help in partial type inference for this operation.
        /// </returns>
        public static CastHelper<T> Cast<T>(this TextParser<T> parser)
            => new CastHelper<T>(parser);

        /// <summary>
        ///     Attempt parsing only if the <paramref name="except"/> parser fails.
        /// </summary>
        /// <typeparam name="T">
        ///     The result type of <paramref name="parser"/>.
        /// </typeparam>
        /// <typeparam name="U">
        ///     The result type of the <paramref name="except"/> parser.
        /// </typeparam>
        /// <param name="parser">
        ///     The parser to use when <paramref name="except"/> fails.
        /// </param>
        /// <param name="except">
        ///     The parser to try first and expect to fail.
        /// </param>
        /// <returns>
        ///     A parser which only successfully parse if the <paramref name="except"/>
        ///     parser fails.
        /// </returns>
        public static TextParser<T> Except<T, U>(this TextParser<T> parser, TextParser<U> except)
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));
            if (except == null) throw new ArgumentNullException(nameof(except));

            // Could be more like: except.Then(s => s.Fail("..")).XOr(parser)
            return i =>
            {
                var r = except(i);
                if (r.HasValue)
                    //return Result.Fail<T>(i, "Excepted parser succeeded.", new[] { "other than the excepted input" });
                    return Result.Empty<T>(i, $"unexpected successful parsing of `{i.Until(r.Remainder)}`");
                return parser(i);
            };
        }

        /// <summary>
        ///     Attempt parsing only if all characters in <paramref name="chars" />
        ///     are missing from input.
        /// </summary>
        /// <param name="parser">
        ///     The parser to use when none of the characters in <paramref name="chars"/>
        ///     are parsed.
        /// </param>
        /// <param name="chars">
        ///     The characters that must not be in input.
        /// </param>
        /// <returns>
        ///     A parser that parse only if all characters in <paramref name="chars"/>
        ///     are missing.
        /// </returns>
        public static TextParser<char> ExceptIn(this TextParser<char> parser, params char[] chars)
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));
            var exceptIn = Character.ExceptIn(chars);

            return i =>
            {
                var r = exceptIn(i);
                if (!r.HasValue)
                    return r;
                return parser(i);
            };
        }

        /// <summary>
        ///     Construct a parser that will fail if the provided <paramref name="parser"/>
        ///     succeeds but did not consume any input.
        /// </summary>
        /// <typeparam name="T">
        ///     The result type of <paramref name="parser"/>.
        /// </typeparam>
        /// <param name="parser">
        ///     A parser.
        /// </param>
        /// <returns>
        ///     A parser that will fail if the provided <paramref name="parser"/>
        ///     succeeds but did not consume any input.
        /// </returns>
        public static TextParser<T> FailIfZeroLength<T>(this TextParser<T> parser)
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));

            return i =>
            {
                var r = parser(i);
                if (r.HasValue && i == r.Remainder)
                    return Result.Empty<T>(i, "zero-length result");
                return r;
            };
        }

        /// <summary>
        ///     Parse a stream of elements containing only one item.
        /// </summary>
        /// <typeparam name="T">
        ///     The result type of <paramref name="parser"/>.
        /// </typeparam>
        /// <param name="parser">
        ///     A parser.
        /// </param>
        /// <returns></returns>
        public static TextParser<IEnumerable<T>> Once<T>(this TextParser<T> parser)
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));

            return parser.Select(r => (IEnumerable<T>)new[] { r });
        }

        // The following redefines Or(...) with specific overloads to automatically
        // convert types. This needs all 3 versions to fix overload resolution.

        /// <summary>
        ///     Construct a parser that tries first the <paramref name="lhs"/> parser,
        ///     and if it fails, applies <paramref name="rhs"/>.
        /// </summary>
        /// <typeparam name="T">
        ///     The type of value being parsed.
        /// </typeparam>
        /// <param name="lhs">
        ///     The first parser to try.
        /// </param>
        /// <param name="rhs">
        ///     The second parser to try.
        /// </param>
        /// <returns>
        ///     The resulting parser.
        /// </returns>
        /// <remarks>
        ///     Or will fail if the first item partially matches this.
        ///     To modify this behavior use <see cref="Combinators.Try{T}(TextParser{T})"/>.
        /// </remarks>

        public static TextParser<T> Or<T>(this TextParser<T> lhs, TextParser<T> rhs)
        {
#if !SPRACHE_COMPATIBILITY
            return Combinators.Or(lhs, rhs);
#else
            if (lhs == null) throw new ArgumentNullException(nameof(lhs));
            if (rhs == null) throw new ArgumentNullException(nameof(rhs));

            // activate backtracking by default for lhs, to mimic same behavior like
            // Or(...) from Sprache
            lhs = lhs.Try();

            var rhsInner = rhs;
            TextParser<T> rhsOuter = i => rhsInner(i);
            TextParser<T> lhsOuter = input =>
            {
                var first = lhs(input);
                // This handles zero-width successful results like Or(...) from Sprache
                if (first.HasValue && input == first.Remainder)
                {
                    rhsInner = i =>
                    {
                        try
                        {
                            var second = rhs(i);
                            if (second.HasValue)
                                return second;
                            return first;
                        }
                        finally
                        {
                            rhsInner = rhs;
                        }
                    };
                    return Result.Empty<T>(first.Remainder, "zero-width result");
                }
                return first;
            };
            return Combinators.Or(lhsOuter, rhsOuter);
#endif
        }

        /// <summary>
        ///     Construct a parser that tries first the <paramref name="lhs"/> parser,
        ///     and if it fails, applies <paramref name="rhs"/>.
        /// </summary>
        /// <typeparam name="T">
        ///     The type of value being parsed.
        /// </typeparam>
        /// <typeparam name="U">
        ///     The common base type of value being parsed.
        /// </typeparam>
        /// <param name="lhs">
        ///     The first parser to try.
        /// </param>
        /// <param name="rhs">
        ///     The second parser to try.
        /// </param>
        /// <param name="_">
        ///     Tag to help the compiler to pick the right overload.
        /// </param>
        /// <returns>
        ///     The resulting parser.
        /// </returns>
        /// <remarks>
        ///     Or will fail if the first item partially matches this.
        ///     To modify this behavior use <see cref="Combinators.Try{T}(TextParser{T})"/>.
        /// </remarks>
        public static TextParser<U> Or<T, U>(this TextParser<T> lhs, TextParser<U> rhs,
            Tags.LHS<T, U> _ = default)
            where T : U
        {
            return Or<U>(lhs.Cast().As<U>(), rhs);
        }

        /// <summary>
        ///     Construct a parser that tries first the <paramref name="lhs"/> parser,
        ///     and if it fails, applies <paramref name="rhs"/>.
        /// </summary>
        /// <typeparam name="T">
        ///     The type of value being parsed.
        /// </typeparam>
        /// <typeparam name="U">
        ///     The common base type of value being parsed.
        /// </typeparam>
        /// <param name="lhs">
        ///     The first parser to try.
        /// </param>
        /// <param name="rhs">
        ///     The second parser to try.
        /// </param>
        /// <param name="_">
        ///     Tag to help the compiler to pick the right overload.
        /// </param>
        /// <returns>
        ///     The resulting parser.
        /// </returns>
        /// <remarks>
        ///     Or will fail if the first item partially matches this.
        ///     To modify this behavior use <see cref="Combinators.Try{T}(TextParser{T})"/>.
        /// </remarks>
        public static TextParser<U> Or<T, U>(this TextParser<U> lhs, TextParser<T> rhs,
            Tags.RHS<U, T> _ = default)
            where T : U
        {
            return Or<U>(lhs, rhs.Cast().As<U>());
        }

        /// <summary>
        ///     Construct a parser that will set the position to the position-aware
        ///     <typeparamref name="T"/> on succsessful match.
        /// </summary>
        /// <typeparam name="T">
        ///     The result type of <paramref name="parser"/>.
        /// </typeparam>
        /// <param name="parser">
        ///     A parser.
        /// </param>
        /// <returns>
        ///     A parser that will set the position to the position-aware
        ///     <typeparamref name="T"/> on succsessful match.
        /// </returns>
        public static TextParser<T> Positioned<T>(this TextParser<T> parser) where T : IPositionAware<T>
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));

            return i =>
            {
                var r = parser(i);

                if (r.HasValue)
                {
                    return Result.Value(r.Value.SetPos(i.Until(r.Remainder)), i, r.Remainder);
                }
                return r;
            };
        }

        /// <summary>
        ///     Parses optional whitespace before and after the provided parser.
        /// </summary>
        /// <typeparam name="T">
        ///     The result type of the provided parser.
        /// </typeparam>
        /// <param name="parser">
        ///     The parser to surround.
        /// </param>
        /// <returns>
        ///     The resulting parser.
        /// </returns>
        public static TextParser<T> Token<T>(this TextParser<T> parser) => parser
            .Between(Character.WhiteSpace.IgnoreMany(), Character.WhiteSpace.IgnoreMany());
    }
}
