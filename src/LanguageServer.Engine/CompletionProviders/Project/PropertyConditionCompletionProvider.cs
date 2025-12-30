using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using LspModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders.Project
{
    using Documents;
    using SemanticModel;
    using Utilities;

    /// <summary>
    ///     Completion provider the Condition attribute of a property element.
    /// </summary>
    public class PropertyConditionCompletionProvider
        : CompletionProvider<ProjectDocument>
    {
        /// <summary>
        ///     Create a new <see cref="PropertyConditionCompletionProvider"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        public PropertyConditionCompletionProvider(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     Provide completions for the specified location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="XmlLocation"/> where completions are requested.
        /// </param>
        /// <param name="document">
        ///     The <see cref="ProjectDocument"/> that contains the <paramref name="location"/>.
        /// </param>
        /// <param name="triggerCharacters">
        ///     The character(s), if any, that triggered completion.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken"/> that can be used to cancel the operation.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}"/> that resolves either a <see cref="CompletionList"/>s, or <c>null</c> if no completions are provided.
        /// </returns>
        public override async Task<CompletionList> ProvideCompletionsAsync(XmlLocation location, ProjectDocument document, string triggerCharacters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(location);

            ArgumentNullException.ThrowIfNull(document);

            var completions = new List<CompletionItem>();

            using (await document.Lock.ReaderLockAsync(cancellationToken))
            {
                if (!location.IsAttributeValue(out XSAttribute conditionAttribute) || conditionAttribute.Name != "Condition")
                    return null;

                if (conditionAttribute.Element.ParentElement?.Name != "PropertyGroup")
                    return null;

                LspModels.Range replaceRange = conditionAttribute.ValueRange.ToLsp();

                completions.Add(new CompletionItem
                {
                    Label = "If not already defined",
                    Detail = "Condition",
                    Documentation = "Only use this property if the property does not already have a value.",
                    TextEdit = new TextEdit
                    {
                        NewText = $"'$({conditionAttribute.Element.Name})' == ''",
                        Range = replaceRange
                    }
                });
            }

            if (completions.Count == 0)
                return null;

            return new CompletionList(completions, isIncomplete: false);
        }
    }
}
