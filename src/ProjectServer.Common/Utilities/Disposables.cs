using System;
using System.Threading.Tasks;

namespace MSBuildProjectTools.ProjectServer.Utilities
{
    /// <summary>
    ///     Factory for commonly-used <see cref="IDisposable"/> implementations.
    /// </summary>
    public static class Disposables
    {
        /// <summary>
        ///     Create an <see cref="IDisposable"/> that invokes a delegate when it is disposed.
        /// </summary>
        /// <param name="disposalAction">
        ///     The delegate to be invoked when the <see cref="IDisposable"/> is disposed.
        /// </param>
        /// <returns>
        ///     The <see cref="IDisposable"/>.
        /// </returns>
        public static IDisposable Action(Action disposalAction) => new ActionDisposable(disposalAction);

        /// <summary>
        ///     Create an <see cref="IAsyncDisposable"/> that invokes a delegate when it is disposed.
        /// </summary>
        /// <param name="disposalAction">
        ///     The delegate to be invoked when the <see cref="IAsyncDisposable"/> is disposed.
        /// </param>
        /// <returns>
        ///     The <see cref="IAsyncDisposable"/>.
        /// </returns>
        public static IAsyncDisposable Action(Func<ValueTask> disposalAction) => new AsyncActionDisposable(disposalAction);

        /// <summary>
        ///     An <see cref="IDisposable"/> implementation that invokes a delegate when it is disposed.
        /// </summary>
        class ActionDisposable
            : DisposableObject
        {
            /// <summary>
            ///     The delegate to be invoked when the <see cref="IDisposable"/> is disposed.
            /// </summary>
            readonly Action _disposalAction;

            /// <summary>
            ///     Create a new <see cref="ActionDisposable"/>.
            /// </summary>
            /// <param name="disposalAction">
            ///     The delegate to be invoked when the <see cref="IDisposable"/> is disposed.
            /// </param>
            public ActionDisposable(Action disposalAction)
            {
                if (disposalAction == null)
                    throw new ArgumentNullException(nameof(disposalAction));

                _disposalAction = disposalAction;
            }

            /// <summary>
            ///     Dispose of resources being used by the object.
            /// </summary>
            /// <param name="disposing">
            ///     Explicit disposal?
            /// </param>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _disposalAction();

                base.Dispose(disposing);
            }
        }

        /// <summary>
        ///     An <see cref="IAsyncDisposable"/> implementation that invokes a delegate when it is disposed.
        /// </summary>
        class AsyncActionDisposable
            : AsyncDisposableObject
        {
            /// <summary>
            ///     The delegate to be invoked when the <see cref="IAsyncDisposable"/> is disposed.
            /// </summary>
            readonly Func<ValueTask> _disposalAction;

            /// <summary>
            ///     Create a new <see cref="AsyncActionDisposable"/>.
            /// </summary>
            /// <param name="disposalAction">
            ///     The delegate to be invoked when the <see cref="IAsyncDisposable"/> is disposed.
            /// </param>
            public AsyncActionDisposable(Func<ValueTask> disposalAction)
            {
                if (disposalAction == null)
                    throw new ArgumentNullException(nameof(disposalAction));

                _disposalAction = disposalAction;
            }

            /// <summary>
            ///     Asynchronously dispose of resources being used by the object.
            /// </summary>
            /// <param name="disposing">
            ///     Explicit disposal?
            /// </param>
            protected override async ValueTask DisposeAsync(bool disposing)
            {
                if (disposing)
                    await _disposalAction();

                await base.DisposeAsync(disposing);
            }
        }
    }
}
