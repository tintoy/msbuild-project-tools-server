using Serilog;
using System;

namespace MSBuildProjectTools.LanguageServer.CompletionProviders.Xml
{
    using Documents;

    /// <summary>
    ///     The base class for completion providers that target documents in XML format.
    /// </summary>
    /// <typeparam name="TDocument">
    ///     The type of <see cref="XmlDocument"/> targeted by the completion provider.
    /// </typeparam>
    public abstract class XmlDocumentCompletionProvider<TDocument>
        : CompletionProvider<TDocument>
        where TDocument : XmlDocument
    {
        /// <summary>
        ///     Create a new <see cref="XmlDocumentCompletionProvider{TDocument}"/>.
        /// </summary>
        /// <param name="logger">
        ///     The application logger.
        /// </param>
        protected XmlDocumentCompletionProvider(ILogger logger)
            : base(logger)
        {
        }

        /// <summary>
        ///     Handle characters (if any) that triggered the completion.
        /// </summary>
        /// <param name="document">
        ///     The <see cref="ProjectDocument"/> that contains the <paramref name="targetRange"/>.
        /// </param>
        /// <param name="triggerCharacters">
        ///     The character(s), if any, that triggered completion.
        /// </param>
        /// <param name="targetRange">
        ///     The target <see cref="Range"/> for completions.
        /// </param>
        /// <returns>
        ///     <c>true</c>, if any trigger characters were handled (i.e. the selection was extended); otherwise, <c>false</c>.
        /// </returns>
        protected override bool HandleTriggerCharacters(string triggerCharacters, TDocument document, ref Range targetRange)
        {
            ArgumentNullException.ThrowIfNull(document);

            // Replace any characters that were typed to trigger the completion.
            if (!String.IsNullOrEmpty(triggerCharacters))
            {
                // The last character typed is implicitly part of the current selection, if it triggered completion.
                int extendSelectionByCharCount = triggerCharacters.Length - 1;
                if (extendSelectionByCharCount > 0)
                {
                    targetRange = document.XmlPositions.ExtendLeft(targetRange, extendSelectionByCharCount);

                    Log.Verbose("Completion was triggered by typing one or more characters; target range will be extended by {TriggerCharacterCount} characters toward start of document (now: {TargetRange}).", triggerCharacters.Length, targetRange);

                    return true;
                }
            }

            return false;
        }
    }
}
