using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace MSBuildProjectTools.LanguageServer
{
    /// <summary>
    ///     Terminates the current process.
    /// </summary>
    /// <remarks>
    ///     Dispose the <see cref="Terminator"/> to cancel termination.
    /// </remarks>
    public class Terminator
        : IDisposable
    {
        /// <summary>
        ///     The delay, after the timeout has elapsed, before 
        /// </summary>
        public static readonly TimeSpan TerminationDelay = TimeSpan.FromSeconds(1);

        /// <summary>
        ///     The source for cancellation tokens used to abort process termination.
        /// </summary>
        readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        /// <summary>
        ///     Create a new <see cref="Terminator"/>.
        /// </summary>
        public Terminator()
        {
            Log = Serilog.Log.Logger.ForContext<Terminator>();
        }

        /// <summary>
        ///     Dispose of resources being used by the terminator.
        /// </summary>
        public void Dispose() => _cancellation.Dispose();

        /// <summary>
        ///     The terminator's logger.
        /// </summary>
        ILogger Log { get; }

        /// <summary>
        ///     Terminate the current process after the specified timeout has elapsed.
        /// </summary>
        /// <param name="timeout">
        ///     The minimum span of time that should elapse before the process is terminated.
        /// </param>
        public async void TerminateAfter(TimeSpan timeout)
        {
            Log.Verbose("Language server process will be forcibly terminated in {Timeout} seconds.", timeout.TotalSeconds);

            CancellationToken cancellationToken = _cancellation.Token;
            _cancellation.CancelAfter(timeout);

            try
            {
                await Task.Delay(timeout, cancellationToken);

                Log.Warning("Timed out after waiting {Timeout} seconds; the language server process will now be forcibly terminated within {TerminationDelay} second(s).",
                    timeout.TotalSeconds,
                    TerminationDelay.TotalSeconds
                );

                // Last-ditch effort to flush pending log entries.
                Serilog.Log.CloseAndFlush();
                await Task.Delay(
                    TimeSpan.FromSeconds(1)
                );

                Terminate();
            }
            catch (OperationCanceledException)
            {
                Log.Verbose("Termination of the language server process has been cancelled.");
            }
        }

        /// <summary>
        ///     Terminate the current process.
        /// </summary>
        void Terminate() => Environment.Exit(9);
    }
}
