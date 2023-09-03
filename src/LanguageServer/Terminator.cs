using Serilog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

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
        private static readonly int s_currentProcessId;

        /// <summary>
        ///     A <see cref="Process"/> representing the parent process.
        /// </summary>
        private Process _parentProcess;

        /// <summary>
        ///     The terminator's logger.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        ///     Type initializer for <see cref="Terminator"/>.
        /// </summary>
        static Terminator()
        {
            using Process currentProcess = Process.GetCurrentProcess();
            s_currentProcessId = currentProcess.Id;
        }

        /// <summary>
        ///     Create a new <see cref="Terminator"/>.
        /// </summary>
        public Terminator()
        {
            _logger = Log.Logger.ForContext<Terminator>();
        }

        /// <summary>
        ///     Dispose of resources being used by the terminator.
        /// </summary>
        public void Dispose()
        {
            if (_parentProcess != null)
            {
                _parentProcess.Dispose();
                _parentProcess = null;
            }
        }

        /// <summary>
        /// Initialize the process terminator.
        /// </summary>
        /// <param name="parentProcessId">
        ///     The process Id (PID) of the parent process that launched the language server.
        /// </param>
        public void Initialize(int parentProcessId)
        {
            if (_parentProcess != null)
            {
                _logger.Warning("The language server process (PID:{PID}) is now watching its parent process (PID:{ParentPID}) and will automatically terminate if the parent process exits.", s_currentProcessId, _parentProcess.Id);

                _parentProcess.EnableRaisingEvents = false;
                _parentProcess.Exited -= ParentProcess_Exit;
                _parentProcess.Dispose();
                _parentProcess = null;
            }

            _parentProcess = Process.GetProcessById(parentProcessId);
            _parentProcess.Exited += ParentProcess_Exit;
            _parentProcess.EnableRaisingEvents = true;

            _logger.Information("The language server (PID:{PID}) is now watching its parent process (PID:{ParentPID}) and will automatically terminate if the parent process exits.", s_currentProcessId, _parentProcess.Id);

            // Handle the case where the parent process has already exited.
            if (_parentProcess.HasExited)
                ParentProcess_Exit(_parentProcess, EventArgs.Empty);
        }

        /// <summary>
        ///     Called when the language server's parent process has exited.
        /// </summary>
        /// <param name="sender">
        ///     The event sender.
        /// </param>
        /// <param name="args">
        ///     The event arguments.
        /// </param>
        async void ParentProcess_Exit(object sender, EventArgs args)
        {
            _logger.Warning("Parent process (PID:{ParentPID}) has exited; the language server (PID:{PID}) will immediately self-terminate.", _parentProcess.Id, s_currentProcessId);

            // Last-ditch effort to flush pending log entries.
            (_logger as IDisposable)?.Dispose();
            Serilog.Log.CloseAndFlush();

            // Wait for log flush.
            await Task.Delay(
                TimeSpan.FromSeconds(1)
            );

            Terminate();
        }

        /// <summary>
        ///     Terminate the current process.
        /// </summary>
        static void Terminate() => Environment.Exit(9);
    }
}
