using Microsoft.Extensions.Logging;
using System;

namespace MSBuildProjectTools.ProjectServer.Engine.Models
{
    public record class LoggingConfiguration(LogLevel Level, string? LogFile, SeqLoggingConfiguration Seq, bool Trace)
    {
        public static readonly LoggingConfiguration Default = new LoggingConfiguration(
            Level: LogLevel.Information,
            LogFile: null,
            Seq: SeqLoggingConfiguration.Default,
            Trace: false
        );

        public LoggingConfiguration Merge(LoggingConfiguration loggingConfiguration)
        {
            if (loggingConfiguration == null)
                throw new ArgumentNullException(nameof(loggingConfiguration));

            if (Equals(loggingConfiguration))
                return this;

            return this with
            {
                Level = loggingConfiguration.Level,
                LogFile = loggingConfiguration.LogFile,
                Seq = loggingConfiguration.Seq,
                Trace = loggingConfiguration.Trace,
            };
        }
    }
}
