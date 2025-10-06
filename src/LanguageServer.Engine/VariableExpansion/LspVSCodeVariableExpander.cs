using OmniSharp.Extensions.LanguageServer.Server;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MSBuildProjectTools.LanguageServer.VariableExpansion
{
    using CustomProtocol;
    using Utilities;

    /// <summary>
    ///     An implementation of <see cref="IExpandVariables"/> that expands variables via LSP.
    /// </summary>
    class LspVSCodeVariableExpander
        : IExpandVariables
    {
        /// <summary>
        ///     The LSP <see cref="ILanguageServer"/>.
        /// </summary>
        readonly ILanguageServer _languageServer;

        /// <summary>
        ///     Create a new <see cref="LspVSCodeVariableExpander"/>.
        /// </summary>
        /// <param name="languageServer">
        ///     The LSP <see cref="ILanguageServer"/>.
        /// </param>
        public LspVSCodeVariableExpander(ILanguageServer languageServer)
        {
            if (languageServer == null)
                throw new ArgumentNullException(nameof(languageServer));

            _languageServer = languageServer;
        }

        /// <summary>
        ///     Expand the specified variables.
        /// </summary>
        /// <param name="projectDocumentUri">
        ///     The document URI of the calling project.
        /// </param>
        /// <param name="variables">
        ///     The variables to expand (in-place).
        /// </param>
        public async Task ExpandVariables(Uri projectDocumentUri, Dictionary<string, string> variables)
        {
            if (projectDocumentUri == null)
                throw new ArgumentNullException(nameof(projectDocumentUri));

            if (variables == null)
                throw new ArgumentNullException(nameof(variables));

            var request = new ExpandVariablesRequest();
            request.Variables.AddRange(variables);

            ExpandVariablesResponse response = await _languageServer.SendRequest<ExpandVariablesRequest, ExpandVariablesResponse>("variables/expand", request);
            foreach ((string variableName, string variableValue) in response.Variables)
                variables[variableName] = variableValue;
        }
    }
}
