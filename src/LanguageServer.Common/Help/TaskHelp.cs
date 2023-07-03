using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MSBuildProjectTools.LanguageServer.Help
{
    /// <summary>
    ///     Help information for an MSBuild task.
    /// </summary>
    public class TaskHelp
    {
        /// <summary>
        ///     A description of the task.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        ///     A link to the task's documentation (if available).
        /// </summary>
        public string HelpLink { get; init; }

        /// <summary>
        ///     The task's parameters.
        /// </summary>
        public SortedDictionary<string, TaskParameterHelp> Parameters { get; init; }
    }

    /// <summary>
    ///     Help information for an MSBuild task parameter.
    /// </summary>
    public class TaskParameterHelp
    {
        /// <summary>
        ///     A description of the task parameter.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        ///     A link to the task parameter's documentation (if available).
        /// </summary>
        public string HelpLink { get; init; }

        /// <summary>
        ///     A description of the task parameter data-type.
        /// </summary>
        [JsonPropertyName("type")]
        public string TypeDescription { get; init; }
    }
}
