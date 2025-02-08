using Microsoft.Extensions.Logging;
using System;

namespace MSBuildProjectTools.ProjectServer.Engine.Models
{
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
