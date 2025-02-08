using Microsoft.Extensions.Logging;
using System;

namespace MSBuildProjectTools.ProjectServer.Protocol.Contracts
{
    public record class ProjectServerConfiguration(LoggingConfiguration Logging)
    {
        public static readonly ProjectServerConfiguration Default = new ProjectServerConfiguration(
            Logging: LoggingConfiguration.Default
        );

        public ProjectServerConfiguration Merge(ProjectServerConfiguration projectServerConfiguration)
        {
            if (projectServerConfiguration == null)
                throw new ArgumentNullException(nameof(projectServerConfiguration));

            if (Equals(projectServerConfiguration))
                return this;

            return this with
            {
                Logging = projectServerConfiguration.Logging,
            };
        }
    }

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

    public record class SeqLoggingConfiguration(LogLevel Level, Uri? Url, string? ApiKey)
    {
        public static readonly SeqLoggingConfiguration Default = new SeqLoggingConfiguration(
            Level: LogLevel.Information,
            Url: null,
            ApiKey: null
        );

        public SeqLoggingConfiguration Merge(SeqLoggingConfiguration seqLoggingConfiguration)
        {
            if (seqLoggingConfiguration == null)
                throw new ArgumentNullException(nameof(seqLoggingConfiguration));

            if (Equals(seqLoggingConfiguration))
                return this;

            return this with
            {
                Level = seqLoggingConfiguration.Level,
                Url = seqLoggingConfiguration.Url,
                ApiKey = seqLoggingConfiguration.ApiKey,
            };
        }
    }
}
