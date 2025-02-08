using System;
using System.Threading;

namespace MSBuildProjectTools.ProjectServer.Utilities
{
    /// <summary>
    ///     Base class for objects that implement <see cref="IDisposable"/>.
    /// </summary>
    public abstract class DisposableObject
        : IDisposable
    {
        /// <summary>
        ///     Has the object been disposed?
        /// </summary>
        int _isDisposed;

        /// <summary>
        ///     Create a new <see cref="DisposableObject"/>.
        /// </summary>
        protected DisposableObject()
        {
        }

        /// <summary>
        ///     Finaliser for <see cref="DisposableObject"/>.
        /// </summary>
        ~DisposableObject()
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

            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Dispose of resources being used by the object.
        /// </summary>
        /// <param name="disposing">
        ///     Explicit disposal?
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
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
