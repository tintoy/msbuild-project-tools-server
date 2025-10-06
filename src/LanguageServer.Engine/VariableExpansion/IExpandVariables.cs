using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.VariableExpansion
{
    /// <summary>
    ///     Represents a facility for expanding variables.
    /// </summary>
    internal interface IExpandVariables
    {
        /// <summary>
        ///     Expand the specified variables.
        /// </summary>
        /// <param name="projectDocumentUri">
        ///     The document URI of the calling project.
        /// </param>
        /// <param name="variables">
        ///     The variables to expand (in-place).
        /// </param>
        Task ExpandVariables(Uri projectDocumentUri, Dictionary<string, string> variables);
    }

}
