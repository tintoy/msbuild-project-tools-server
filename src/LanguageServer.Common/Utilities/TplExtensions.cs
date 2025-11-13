using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Extension methods for working with TPL
    /// </summary>
    public static class TplExtensions
    {
        private static CancellationTokenRegistration CanceledByInternal(this TaskCompletionSource tcs, CancellationTokenSource cts, bool dispose = false)
        {
            if (!dispose)
                return cts.Token.Register(
                    (state, token) => ((TaskCompletionSource)state).TrySetCanceled(token),
                    tcs);
            else
                return cts.Token.Register(
                    (state, token) =>
                    {
                        var (_tcs, _cts) =
                            ((TaskCompletionSource, CancellationTokenSource))state;
                        _tcs.TrySetCanceled(token);
                        _cts.Dispose();
                    },
                    (tcs, cts));
        }
        
        private static CancellationTokenRegistration CanceledByInternal<TResult>(this TaskCompletionSource<TResult> tcs, CancellationTokenSource cts, bool dispose = false)
        {
            if (!dispose)
                return cts.Token.Register(
                    (state, token) => ((TaskCompletionSource<TResult>)state).TrySetCanceled(token),
                    tcs);
            else
                return cts.Token.Register(
                    (state, token) =>
                    {
                        var (_tcs, _cts) =
                            ((TaskCompletionSource<TResult>, CancellationTokenSource))state;
                        _tcs.TrySetCanceled(token);
                        _cts.Dispose();
                    },
                    (tcs, cts));
        }

        /// <summary>
        ///     Registers a <see cref="TaskCompletionSource"/> to be canceled, when
        ///     the <see cref="CancellationTokenSource"/> is canceled.
        /// </summary>
        /// <param name="tcs">
        ///     A task completion source.
        /// </param>
        /// <param name="cts">
        ///     A cancellation token source.
        /// </param>
        /// <returns>
        ///     A <see cref="CancellationTokenRegistration"/>, that can be used
        ///     to unregister the cancellation delegate.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Either <paramref name="tcs"/> or <paramref name="cts"/> is <c>null</c>.
        /// </exception>
        public static CancellationTokenRegistration CanceledBy(this TaskCompletionSource tcs, CancellationTokenSource cts)
        {
            ArgumentNullException.ThrowIfNull(tcs);
            ArgumentNullException.ThrowIfNull(cts);

            return CanceledByInternal(tcs, cts);
        }

        /// <summary>
        ///     Registers a <see cref="TaskCompletionSource{TResult}"/> to be canceled, when
        ///     the <see cref="CancellationTokenSource"/> is canceled.
        /// </summary>
        /// <typeparam name="TResult">
        ///     The type of the result value associated with this <see cref="TaskCompletionSource{TResult}"/>.
        /// </typeparam>
        /// <param name="tcs">
        ///     A task completion source.
        /// </param>
        /// <param name="cts">
        ///     A cancellation token source.
        /// </param>
        /// <returns>
        ///     A <see cref="CancellationTokenRegistration"/>, that can be used
        ///     to unregister the cancellation delegate.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Either <paramref name="tcs"/> or <paramref name="cts"/> is <c>null</c>.
        /// </exception>
        public static CancellationTokenRegistration CanceledBy<TResult>(this TaskCompletionSource<TResult> tcs, CancellationTokenSource cts)
        {
            ArgumentNullException.ThrowIfNull(tcs);
            ArgumentNullException.ThrowIfNull(cts);

            return CanceledByInternal(tcs, cts);
        }

        /// <summary>
        ///     Schedules a Cancel operation on a new <see cref="CancellationTokenSource"/> which
        ///     cancels the <see cref="TaskCompletionSource"/>.
        /// </summary>
        /// <param name="tcs">
        ///     A task completion source.
        /// </param>
        /// <param name="millisecondsDelay">
        ///     The time span to wait before canceling this <see cref="CancellationTokenSource"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     The exception thrown when <paramref name="millisecondsDelay"/> is less than -1.
        /// </exception>
        /// <remarks>
        /// <para>
        ///     The countdown for the millisecondsDelay starts during this call.  When the millisecondsDelay expires,
        ///     this <see cref="TaskCompletionSource"/> is canceled, if it has
        ///     not been canceled already.
        /// </para>
        /// <para>
        ///     Subsequent calls to CancelAfter will not reset the millisecondsDelay for this
        ///     <see cref="TaskCompletionSource"/>, if it has not been
        ///     canceled already. It will schedule another Cancel operation with a new time span.
        /// </para>
        /// </remarks>
        public static CancellationTokenRegistration CancelAfter(this TaskCompletionSource tcs, int millisecondsDelay)
        {
            ArgumentNullException.ThrowIfNull(tcs);

            var cts = new CancellationTokenSource(millisecondsDelay);
            return CanceledByInternal(tcs, cts, true);
        }

        /// <summary>
        ///     Schedules a Cancel operation on a new <see cref="CancellationTokenSource"/> which
        ///     cancels the <see cref="TaskCompletionSource"/>.
        /// </summary>
        /// <param name="tcs">
        ///     A task completion source.
        /// </param>
        /// <param name="delay">
        ///     The time span to wait before canceling this <see cref="CancellationTokenSource"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     The <paramref name="delay"/> is less than -1 or greater than maximum allowed timer duration.
        /// </exception>
        /// <remarks>
        /// <para>
        ///     The countdown for the delay starts during this call.  When the delay expires,
        ///     this <see cref="TaskCompletionSource"/> is canceled, if it has
        ///     not been canceled already.
        /// </para>
        /// <para>
        ///     Subsequent calls to CancelAfter will not reset the delay for this
        ///     <see cref="TaskCompletionSource"/>, if it has not been canceled already.
        ///     It will schedule another Cancel operation with a new time span.
        /// </para>
        /// </remarks>
        public static CancellationTokenRegistration CancelAfter(this TaskCompletionSource tcs, TimeSpan delay)
        {
            ArgumentNullException.ThrowIfNull(tcs);

            var cts = new CancellationTokenSource(delay);
            return CanceledByInternal(tcs, cts, true);
        }

        /// <summary>
        ///     Schedules a Cancel operation on a new <see cref="CancellationTokenSource"/> which
        ///     cancels the <see cref="TaskCompletionSource"/>.
        /// </summary>
        /// <typeparam name="TResult">
        ///     The type of the result value associated with this <see cref="TaskCompletionSource{TResult}"/>.
        /// </typeparam>
        /// <param name="tcs">
        ///     A task completion source.
        /// </param>
        /// <param name="millisecondsDelay">
        ///     The time span to wait before canceling this <see cref="CancellationTokenSource"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     The exception thrown when <paramref name="millisecondsDelay"/> is less than -1.
        /// </exception>
        /// <remarks>
        /// <para>
        ///     The countdown for the millisecondsDelay starts during this call.  When the millisecondsDelay expires,
        ///     this <see cref="TaskCompletionSource"/> is canceled, if it has
        ///     not been canceled already.
        /// </para>
        /// <para>
        ///     Subsequent calls to CancelAfter will not reset the millisecondsDelay for this
        ///     <see cref="TaskCompletionSource"/>, if it has not been
        ///     canceled already. It will schedule another Cancel operation with a new time span.
        /// </para>
        /// </remarks>
        public static CancellationTokenRegistration CancelAfter<TResult>(this TaskCompletionSource<TResult> tcs, int millisecondsDelay)
        {
            ArgumentNullException.ThrowIfNull(tcs);

            var cts = new CancellationTokenSource(millisecondsDelay);
            return CanceledByInternal(tcs, cts, true);
        }

        /// <summary>
        ///     Schedules a Cancel operation on a new <see cref="CancellationTokenSource"/> which
        ///     cancels the <see cref="TaskCompletionSource"/>.
        /// </summary>
        /// <typeparam name="TResult">
        ///     The type of the result value associated with this <see cref="TaskCompletionSource{TResult}"/>.
        /// </typeparam>
        /// <param name="tcs">
        ///     A task completion source.
        /// </param>
        /// <param name="delay">
        ///     The time span to wait before canceling this <see cref="CancellationTokenSource"/>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     The <paramref name="delay"/> is less than -1 or greater than maximum allowed timer duration.
        /// </exception>
        /// <remarks>
        /// <para>
        ///     The countdown for the delay starts during this call.  When the delay expires,
        ///     this <see cref="TaskCompletionSource"/> is canceled, if it has
        ///     not been canceled already.
        /// </para>
        /// <para>
        ///     Subsequent calls to CancelAfter will not reset the delay for this
        ///     <see cref="TaskCompletionSource"/>, if it has not been canceled already.
        ///     It will schedule another Cancel operation with a new time span.
        /// </para>
        /// </remarks>
        public static CancellationTokenRegistration CancelAfter<TResult>(this TaskCompletionSource<TResult> tcs, TimeSpan delay)
        {
            ArgumentNullException.ThrowIfNull(tcs);

            var cts = new CancellationTokenSource(delay);
            return CanceledByInternal(tcs, cts, true);
        }
    }
}
