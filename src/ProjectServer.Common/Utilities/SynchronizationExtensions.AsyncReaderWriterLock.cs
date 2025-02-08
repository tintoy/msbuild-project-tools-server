using DotNext.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.ProjectServer.Utilities
{
    /// <summary>
    ///     Extension methods for synchronisation (i.e. concurrency) scenarios.
    /// </summary>
    public static partial class SynchronizationExtensions
    {
        /// <summary>
        ///		Asynchronously enter the read lock and create a scope which, when disposed, will exit the read lock.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="AsyncReaderWriterLock"/> that controls the read lock.
        /// </param>
        /// <param name="cancellationToken">
        ///		A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        ///		An <see cref="AsyncReadLockScope"/> representing the lock scope.
        /// </returns>
        public static async Task<AsyncReadLockScope> EnterReadScopeAsync(this AsyncReaderWriterLock readerWriterLock, CancellationToken cancellationToken)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            try
            {
                await readerWriterLock.EnterReadLockAsync(cancellationToken);
            }
            catch (OperationCanceledException canceled)
            {
                throw new OperationCanceledException("Failed to acquire the read lock.",
                    innerException: canceled,
                    token: canceled.CancellationToken
                );
            }

            try
            {
                return new AsyncReadLockScope(readerWriterLock);
            }
            catch (Exception)
            {
                if (readerWriterLock.IsReadLockHeld)
                    readerWriterLock.Release();

                throw;
            }
        }

        /// <summary>
        ///		Asynchronously enter the write lock and create a scope which, when disposed, will exit the write lock.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="AsyncReaderWriterLock"/> that controls the write lock.
        /// </param>
        /// <param name="cancellationToken">
        ///		A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        ///		The <see cref="AsyncWriteLockScope"/>.
        /// </returns>
        public static async Task<AsyncWriteLockScope> EnterWriteScopeAsync(this AsyncReaderWriterLock readerWriterLock, CancellationToken cancellationToken)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            try
            {
                await readerWriterLock.EnterWriteLockAsync(cancellationToken);
            }
            catch (OperationCanceledException canceled)
            {
                throw new OperationCanceledException("Failed to acquire the write lock.",
                    innerException: canceled,
                    token: canceled.CancellationToken
                );
            }

            try
            {
                return new AsyncWriteLockScope(readerWriterLock);
            }
            catch (Exception)
            {
                if (readerWriterLock.IsWriteLockHeld)
                    readerWriterLock.Release();

                throw;
            }
        }

        /// <summary>
        ///		Enter the write lock by upgrading from a read lcok and create a scope which, when disposed, will either exit the write lock or downgrade it to a read lock.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="AsyncReaderWriterLock"/> that controls the write lock.
        /// </param>
        /// <param name="cancellationToken">
        ///		A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        /// <returns>
        ///		The <see cref="AsyncWriteLockScope"/>.
        /// </returns>
        internal static async Task<AsyncWriteLockScope> EnterWriteScopeFromReadScopeAsync(this AsyncReaderWriterLock readerWriterLock, CancellationToken cancellationToken)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            try
            {
                await readerWriterLock.UpgradeToWriteLockAsync(cancellationToken);
            }
            catch (OperationCanceledException canceled)
            {
                throw new OperationCanceledException("Failed to acquire the write lock.",
                    innerException: canceled,
                    token: canceled.CancellationToken
                );
            }

            try
            {
                return new AsyncWriteLockScope(readerWriterLock, wasUpgradedFromReadLock: true);
            }
            catch (Exception)
            {
                if (readerWriterLock.IsWriteLockHeld)
                    readerWriterLock.Release();

                throw;
            }
        }
    }

    /// <summary>
    ///		Disposable scope representing a read lock on a <see cref="AsyncReaderWriterLock"/>.
    /// </summary>
    public struct AsyncReadLockScope
        : IDisposable
    {
        /// <summary>
        ///		Has the scope been disposed?
        /// </summary>
        int _isDisposed;

        /// <summary>
        ///		Create a new <see cref="AsyncReadLockScope"/>.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="AsyncReaderWriterLock"/> that controls the lock.
        /// </param>
        internal AsyncReadLockScope(AsyncReaderWriterLock readerWriterLock)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            if (!readerWriterLock.IsReadLockHeld)
                throw new InvalidOperationException("Cannot create read-lock scope (read lock is not held by the current thread/task).");

            Lock = readerWriterLock;
        }

        /// <summary>
        ///		The <see cref="AsyncReaderWriterLock"/> that controls the lock.
        /// </summary>
        public AsyncReaderWriterLock Lock { get; }

        /// <summary>
        ///		Asynchronously upgrade the read lock to a write lock.
        /// </summary>
        /// <param name="cancellationToken">
        ///		A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
        /// </param>
        /// <returns>
        ///     A <see cref="AsyncWriteLockScope"/> representing the (upgraded) write lock.
        /// </returns>
        public async Task<AsyncWriteLockScope> UpgradeToWriteLockAsync(CancellationToken cancellationToken)
        {
            if (Lock == null)
                throw new InvalidOperationException($"Cannot upgrade to write lock from {nameof(AsyncReadLockScope)} because its lock is null.");

            return await Lock.EnterWriteScopeFromReadScopeAsync(cancellationToken);
        }

        /// <summary>
        ///		Exit the read lock.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
                return; // Already disposed.

            if (Lock == null)
                return;

            if (Lock.IsReadLockHeld)
                Lock.Release();
        }
    }

    /// <summary>
    ///		Disposable scope representing a write lock on a <see cref="AsyncReaderWriterLock"/>.
    /// </summary>
    public struct AsyncWriteLockScope
        : IDisposable
    {
        /// <summary>
        ///		Has the scope been disposed?
        /// </summary>
        int _isDisposed;

        /// <summary>
        ///		Create a new <see cref="AsyncWriteLockScope"/>.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="AsyncReaderWriterLock"/> that controls the lock.
        /// </param>
        /// <param name="wasUpgradedFromReadLock">
        ///     Was the write lock upgraded from a read lock?
        /// </param>
        internal AsyncWriteLockScope(AsyncReaderWriterLock readerWriterLock, bool wasUpgradedFromReadLock = false)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            if (!readerWriterLock.IsWriteLockHeld)
                throw new InvalidOperationException("Cannot create write-lock scope (write lock is not held by the current thread/task).");

            Lock = readerWriterLock;
            WasUpgradedFromReadLock = wasUpgradedFromReadLock;
        }

        /// <summary>
        ///		The <see cref="AsyncReaderWriterLock"/> that controls the lock.
        /// </summary>
        public AsyncReaderWriterLock Lock { get; }

        /// <summary>
        ///     Was the write lock upgraded from a read lock?
        /// </summary>
        public bool WasUpgradedFromReadLock {  get; }

        /// <summary>
        ///		Exit the write lock.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
                return; // Already disposed.

            if (Lock == null)
                return;

            if (Lock.IsWriteLockHeld)
            {
                if (WasUpgradedFromReadLock)
                    Lock.DowngradeFromWriteLock();
                else
                    Lock.Release();
            }
        }
    }
}
