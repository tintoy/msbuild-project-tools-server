using System;
using System.Threading;

namespace MSBuildProjectTools.ProjectServer.Utilities
{
    /// <summary>
    ///     Extension methods for synchronisation (i.e. concurrency) scenarios.
    /// </summary>
    public static partial class SynchronizationExtensions
    {
        /// <summary>
        ///		Enter the read lock and create a scope which, when disposed, will exit the read lock.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the read lock.
        /// </param>
        /// <param name="timeout">
        ///		The span of time to wait to acquire the lock.
        /// </param>
        /// <returns>
        ///		The <see cref="ReadLockSlimScope"/>.
        /// </returns>
        public static ReadLockSlimScope EnterReadScope(this ReaderWriterLockSlim readerWriterLock, TimeSpan timeout)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            bool haveLock = readerWriterLock.TryEnterReadLock(timeout);
            if (!haveLock)
                throw new TimeoutException($"Failed to acquire the read lock after {timeout.TotalMilliseconds}ms.");

            try
            {
                return new ReadLockSlimScope(readerWriterLock);
            }
            catch (Exception)
            {
                readerWriterLock.ExitReadLock();

                throw;
            }
        }

        /// <summary>
        ///		Enter the upgradeable read lock and create a scope which, when disposed, will exit the upgradeable read lock.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the upgradeable read lock.
        /// </param>
        /// <param name="timeout">
        ///		The span of time to wait to acquire the lock.
        /// </param>
        /// <returns>
        ///		The <see cref="UpgradeableReadLockSlimScope"/>.
        /// </returns>
        public static UpgradeableReadLockSlimScope EnterUpgradeableReadScope(this ReaderWriterLockSlim readerWriterLock, TimeSpan timeout)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            bool haveLock = readerWriterLock.TryEnterUpgradeableReadLock(timeout);
            if (!haveLock)
                throw new TimeoutException($"Failed to acquire the upgradeable read lock after {timeout.TotalMilliseconds}ms.");

            try
            {
                return new UpgradeableReadLockSlimScope(readerWriterLock);
            }
            catch (Exception)
            {
                readerWriterLock.ExitUpgradeableReadLock();

                throw;
            }
        }

        /// <summary>
        ///		Enter the write lock and create a scope which, when disposed, will exit the write lock.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the write lock.
        /// </param>
        /// <param name="timeout">
        ///		The span of time to wait to acquire the lock.
        /// </param>
        /// <returns>
        ///		The <see cref="WriteLockSlimScope"/>.
        /// </returns>
        public static WriteLockSlimScope EnterWriteScope(this ReaderWriterLockSlim readerWriterLock, TimeSpan timeout)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            bool haveLock = readerWriterLock.TryEnterWriteLock(timeout);
            if (!haveLock)
                throw new TimeoutException($"Failed to acquire the write lock after {timeout.TotalMilliseconds}ms.");

            try
            {
                return new WriteLockSlimScope(readerWriterLock);
            }
            catch (Exception)
            {
                readerWriterLock.ExitReadLock();

                throw;
            }
        }
    }

    /// <summary>
    ///		Disposable scope representing a read lock on a <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    public struct ReadLockSlimScope
        : IDisposable
    {
        /// <summary>
        ///		Has the scope been disposed?
        /// </summary>
        int _isDisposed;

        /// <summary>
        ///		Create a new <see cref="ReadLockSlimScope"/>.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the lock.
        /// </param>
        internal ReadLockSlimScope(ReaderWriterLockSlim readerWriterLock)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            if (!readerWriterLock.IsReadLockHeld)
                throw new InvalidOperationException("Cannot create read-lock scope (read lock is not held by the current thread).");

            Lock = readerWriterLock;
        }

        /// <summary>
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the lock.
        /// </summary>
        public ReaderWriterLockSlim Lock { get; }

        /// <summary>
        ///		Exit the read lock.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
                return; // Already disposed.

            Lock?.ExitReadLock();
        }
    }

    /// <summary>
    ///		Disposable scope representing an upgradeable read lock on a <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    public struct UpgradeableReadLockSlimScope
        : IDisposable
    {
        /// <summary>
        ///		Has the scope been disposed?
        /// </summary>
        int _isDisposed;

        /// <summary>
        ///		Create a new <see cref="UpgradeableReadLockSlimScope"/>.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the lock.
        /// </param>
        internal UpgradeableReadLockSlimScope(ReaderWriterLockSlim readerWriterLock)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            if (!readerWriterLock.IsUpgradeableReadLockHeld)
                throw new InvalidOperationException("Cannot create upgradeable read-lock scope (upgradeable read lock is not held by the current thread).");

            Lock = readerWriterLock;
        }

        /// <summary>
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the lock.
        /// </summary>
        public ReaderWriterLockSlim Lock { get; }

        /// <summary>
        ///		Upgrade the read lock to a write lock.
        /// </summary>
        /// <param name="timeout">
        ///		The span of time to wait to acquire the write lock.
        /// </param>
        /// <returns></returns>
        public WriteLockSlimScope UpgradeToWriteLock(TimeSpan timeout) => Lock.EnterWriteScope(timeout);

        /// <summary>
        ///		Exit the upgradeable read lock.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
                return; // Already disposed.

            Lock?.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    ///		Disposable scope representing a write lock on a <see cref="ReaderWriterLockSlim"/>.
    /// </summary>
    public struct WriteLockSlimScope
        : IDisposable
    {
        /// <summary>
        ///		Has the scope been disposed?
        /// </summary>
        int _isDisposed;

        /// <summary>
        ///		Create a new <see cref="WriteLockSlimScope"/>.
        /// </summary>
        /// <param name="readerWriterLock">
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the lock.
        /// </param>
        internal WriteLockSlimScope(ReaderWriterLockSlim readerWriterLock)
        {
            if (readerWriterLock == null)
                throw new ArgumentNullException(nameof(readerWriterLock));

            if (!readerWriterLock.IsWriteLockHeld)
                throw new InvalidOperationException("Cannot create write-lock scope (write lock is not held by the current thread).");

            Lock = readerWriterLock;
        }

        /// <summary>
        ///		The <see cref="ReaderWriterLockSlim"/> that controls the lock.
        /// </summary>
        public ReaderWriterLockSlim Lock { get; }

        /// <summary>
        ///		Exit the write lock.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
                return; // Already disposed.

            Lock?.ExitWriteLock();
        }
    }
}
