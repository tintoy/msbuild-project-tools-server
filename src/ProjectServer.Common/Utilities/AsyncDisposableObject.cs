using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.ProjectServer.Utilities
{
    /// <summary>
    ///     Base class for objects that implement <see cref="IAsyncDisposable"/>.
    /// </summary>
    public abstract class AsyncDisposableObject
        : IAsyncDisposable, IDisposable
    {
        /// <summary>
        ///     Has the object been disposed?
        /// </summary>
        int _isDisposed;

        /// <summary>
        ///     Create a new <see cref="AsyncDisposableObject"/>.
        /// </summary>
        protected AsyncDisposableObject()
        {
        }

        /// <summary>
        ///     Finaliser for <see cref="AsyncDisposableObject"/>.
        /// </summary>
        ~AsyncDisposableObject()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Has the object been disposed?
        /// </summary>
        protected bool IsDisposed => _isDisposed != 0;

        /// <summary>
        ///     Dispose of resources being used by the object.
        /// </summary>
        public void Dispose()
        {
            int wasDisposed = Interlocked.Exchange(ref _isDisposed, 1);
            if (wasDisposed != 0)
                return;

            Dispose(disposing: true);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Asynchronously dispose of resources being used by the object.
        /// </summary>
        /// <returns>
        ///     A <see cref="ValueTask"/> representing the asynchronous disposal operation.
        /// </returns>
        public ValueTask DisposeAsync()
        {
            int wasDisposed = Interlocked.Exchange(ref _isDisposed, 1);
            if (wasDisposed != 0)
                return ValueTask.CompletedTask;

            return DisposeAsyncCore(disposing: true);
        }

        /// <summary>
        ///     Dispose of resources being used by the object.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        ///     The core (actually-async) implementation of <see cref="DisposeAsync()"/>.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        /// <returns>
        ///     A <see cref="ValueTask"/> representing the asynchronous disposal operation.
        /// </returns>
        async ValueTask DisposeAsyncCore(bool disposing)
        {
            await DisposeAsync(disposing);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Asynchronously dispose of resources being used by the object.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected virtual ValueTask DisposeAsync(bool disposing)
        {
            Dispose(disposing);

            return ValueTask.CompletedTask;
        }

        /// <summary>
        ///     Check if the object has been disposed and, if so, throw an <see cref="ObjectDisposedException"/>.
        /// </summary>
        protected void CheckDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(objectName: GetType().FullName);
        }
    }
}
