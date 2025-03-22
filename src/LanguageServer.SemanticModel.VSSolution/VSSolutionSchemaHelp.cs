using VSSolutionProjectTools.LanguageServer.Help;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace VSSolutionProjectTools.LanguageServer.SemanticModel
{
    // TODO: Generate JSON for help using the XSD for the .slnx format (https://github.com/microsoft/vs-solutionpersistence/blob/main/src/Microsoft.VisualStudio.SolutionPersistence/Serializer/Xml/Slnx.xsd).

    /// <summary>
    ///     Help content for objects in the Visual Studio Solution XML schema.
    /// </summary>
    public static class VSSolutionSchemaHelp
    {
        /// <summary>
        ///     Type initializer for <see cref="VSSolutionSchemaHelp"/>.
        /// </summary>
        static VSSolutionSchemaHelp()
        {
            var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var helpDirectory = Path.Combine(currentAssemblyDirectory, "help");

            var jsonOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
            };

            // TODO: Load help.
            ElementHelp = new SortedDictionary<string, ElementHelp>();
        }

        /// <summary>
        ///     Help for Visual Studio Solution elements.
        /// </summary>
        static SortedDictionary<string, ElementHelp> ElementHelp { get; }
}
