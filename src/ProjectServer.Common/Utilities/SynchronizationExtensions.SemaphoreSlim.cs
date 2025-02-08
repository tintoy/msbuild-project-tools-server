using System;
using System.Threading;
using System.Threading.Tasks;

namespace MSBuildProjectTools.ProjectServer.Utilities
{
    /// <summary>
    ///     Extension methods for synchronisation (i.e. concurrency) scenarios.
    /// </summary>
	/// <remarks>
	///     NOTE: <see cref="SemaphoreSlim"/> does not support reentrancy; plan around this or use an alternative mechanism for synchronisation.
	/// </remarks>
    public static partial class SynchronizationExtensions
    {
        /// <summary>
        ///     Acquire the semaphore, perform the specified action, then release the semaphore.
        /// </summary>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="action"></param>
        public static void Execute(this SemaphoreSlim semaphore, Action action) => semaphore.Execute(DefaultLockTimeout, action);

        /// <summary>
        ///     Acquire the semaphore, perform the specified action, then release the semaphore.
        /// </summary>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="timeout">
        ///     The span of time to wait to acquire the semaphore.
        /// </param>
        /// <param name="action">
        ///     The <see cref="Action"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        public static void Execute(this SemaphoreSlim semaphore, TimeSpan timeout, Action action)
        {
            if (semaphore == null)
                throw new ArgumentNullException(nameof(semaphore));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            bool haveLock = semaphore.Wait(timeout);
            if (!haveLock)
                throw new TimeoutException($"Failed to acquire the lock after {timeout.TotalMilliseconds}ms.");

            try
            {
                action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        ///     Acquire the semaphore, perform the specified action, then release the semaphore.
        /// </summary>
        /// <typeparam name="TResult">
        ///     The action result type.
        /// </typeparam>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="action">
        ///     The <see cref="Func{TResult}"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        /// <returns>
        ///     The action result.
        /// </returns>
        public static TResult Execute<TResult>(this SemaphoreSlim semaphore, Func<TResult> action) => semaphore.Execute(DefaultLockTimeout, action);

        /// <summary>
        ///     Acquire the semaphore, perform the specified action, then release the semaphore.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="timeout">
        ///     The span of time to wait to acquire the semaphore.
        /// </param>
        /// <param name="action">
        ///     The <see cref="Func{TResult}"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        /// <returns>
        ///     The action result.
        /// </returns>
        public static TResult Execute<TResult>(this SemaphoreSlim semaphore, TimeSpan timeout, Func<TResult> action)
        {
            if (semaphore == null)
                throw new ArgumentNullException(nameof(semaphore));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            bool haveLock = semaphore.Wait(timeout);
            if (!haveLock)
                throw new TimeoutException($"Failed to acquire the lock after {timeout.TotalMilliseconds}ms.");

            try
            {
                return action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        ///     Acquire the semaphore, perform the specified asyncronous action, then release the semaphore.
        /// </summary>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="asyncAction">
        ///     The asynchronous <see cref="Func{TResult}"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        public static Task ExecuteAsync(this SemaphoreSlim semaphore, Func<Task> asyncAction) => semaphore.ExecuteAsync(DefaultLockTimeout, asyncAction);

        /// <summary>
        ///     Acquire the semaphore, perform the specified asyncronous action, then release the semaphore.
        /// </summary>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="timeout">
        ///     The span of time to wait to acquire the semaphore.
        /// </param>
        /// <param name="asyncAction">
        ///     The asynchronous <see cref="Func{TResult}"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        /// <returns>
        ///     A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        public static async Task ExecuteAsync(this SemaphoreSlim semaphore, TimeSpan timeout, Func<Task> asyncAction)
        {
            if (semaphore == null)
                throw new ArgumentNullException(nameof(semaphore));

            if (asyncAction == null)
                throw new ArgumentNullException(nameof(asyncAction));

            bool haveLock = await semaphore.WaitAsync(timeout);
            if (!haveLock)
                throw new TimeoutException($"Failed to acquire the lock after {timeout.TotalMilliseconds}ms.");

            try
            {
                await asyncAction();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        ///     Acquire the semaphore, perform the specified asyncronous action, then release the semaphore.
        /// </summary>
        /// <typeparam name="TResult">
        ///     The action result type.
        /// </typeparam>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="asyncAction">
        ///     The asynchronous <see cref="Func{TResult}"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        /// <returns>
        ///     The action result.
        /// </returns>
        public static Task<TResult> ExecuteAsync<TResult>(this SemaphoreSlim semaphore, Func<Task<TResult>> asyncAction) => semaphore.ExecuteAsync(DefaultLockTimeout, asyncAction);

        /// <summary>
        ///     Acquire the semaphore, perform the specified asyncronous action, then release the semaphore.
        /// </summary>
        /// <typeparam name="TResult">
        ///     The action result type.
        /// </typeparam>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="timeout">
        ///     The span of time to wait to acquire the semaphore.
        /// </param>
        /// <param name="asyncAction">
        ///     The asynchronous <see cref="Func{TResult}"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        /// <returns>
        ///     The action result.
        /// </returns>
        public static async Task<TResult> ExecuteAsync<TResult>(this SemaphoreSlim semaphore, TimeSpan timeout, Func<Task<TResult>> asyncAction)
        {
            if (semaphore == null)
                throw new ArgumentNullException(nameof(semaphore));

            if (asyncAction == null)
                throw new ArgumentNullException(nameof(asyncAction));

            bool haveLock = await semaphore.WaitAsync(timeout);
            if (!haveLock)
                throw new TimeoutException($"Failed to acquire the lock after {timeout.TotalMilliseconds}ms.");

            try
            {
                return await asyncAction();
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        ///     Acquire the semaphore, perform the specified asyncronous action, then release the semaphore.
        /// </summary>
        /// <typeparam name="TResult">
        ///     The action result type.
        /// </typeparam>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel waiting for the semaphore.
        /// </param>
        /// <param name="asyncAction">
        ///     The asynchronous <see cref="Func{TResult}"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        /// <returns>
        ///     The action result.
        /// </returns>
        public static Task<TResult> ExecuteAsync<TResult>(this SemaphoreSlim semaphore, CancellationToken cancellationToken, Func<CancellationToken, Task<TResult>> asyncAction) => semaphore.ExecuteAsync(DefaultLockTimeout, cancellationToken, asyncAction);

        /// <summary>
        ///     Acquire the semaphore, perform the specified asyncronous action, then release the semaphore.
        /// </summary>
        /// <typeparam name="TResult">
        ///     The action result type.
        /// </typeparam>
        /// <param name="semaphore">
        ///     The <see cref="SemaphoreSlim"/>.
        /// </param>
        /// <param name="timeout">
        ///     The span of time to wait to acquire the semaphore.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel waiting for the semaphore.
        /// </param>
        /// <param name="asyncAction">
        ///     The asynchronous <see cref="Func{TResult}"/> delegate to invoke once the semaphore is acquired.
        /// </param>
        /// <returns>
        ///     The action result.
        /// </returns>
        public static async Task<TResult> ExecuteAsync<TResult>(this SemaphoreSlim semaphore, TimeSpan timeout, CancellationToken cancellationToken, Func<CancellationToken, Task<TResult>> asyncAction)
        {
            if (semaphore == null)
                throw new ArgumentNullException(nameof(semaphore));

            if (asyncAction == null)
                throw new ArgumentNullException(nameof(asyncAction));

            bool haveLock = await semaphore.WaitAsync(timeout, cancellationToken);
            if (!haveLock)
                throw new TimeoutException($"Failed to acquire the lock after {timeout.TotalMilliseconds}ms.");

            try
            {
                return await asyncAction(cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
