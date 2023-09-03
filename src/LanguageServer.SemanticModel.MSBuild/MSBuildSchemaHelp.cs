using MSBuildProjectTools.LanguageServer.Help;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Help content for objects in the MSBuild XML schema.
    /// </summary>
    public static class MSBuildSchemaHelp
    {
        /// <summary>
        ///     Type initializer for <see cref="MSBuildSchemaHelp"/>.
        /// </summary>
        static MSBuildSchemaHelp()
        {
            var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var helpDirectory = Path.Combine(currentAssemblyDirectory, "help");

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
            };

            using (FileStream stream = File.OpenRead(Path.Combine(helpDirectory, "elements.json")))
            {
                s_elementHelp = JsonSerializer.Deserialize<SortedDictionary<string, ElementHelp>>(stream, jsonOptions);
            }

            using (FileStream stream = File.OpenRead(Path.Combine(helpDirectory, "properties.json")))
            {
                s_propertyHelp = JsonSerializer.Deserialize<SortedDictionary<string, PropertyHelp>>(stream, jsonOptions);
            }

            using (FileStream stream = File.OpenRead(Path.Combine(helpDirectory, "items.json")))
            {
                s_itemHelp = JsonSerializer.Deserialize<SortedDictionary<string, ItemHelp>>(stream, jsonOptions);
            }

            s_globalItemMetadataHelp = s_itemHelp.ContainsKey("*") ? s_itemHelp["*"].Metadata : new SortedDictionary<string, string>();

            using (FileStream stream = File.OpenRead(Path.Combine(helpDirectory, "tasks.json")))
            {
                s_taskHelp = JsonSerializer.Deserialize<SortedDictionary<string, TaskHelp>>(stream, jsonOptions);
            }
        }

        /// <summary>
        ///     Help for MSBuild elements.
        /// </summary>
        private static readonly SortedDictionary<string, ElementHelp> s_elementHelp;

        /// <summary>
        ///     Help for MSBuild properties.
        /// </summary>
        private static readonly SortedDictionary<string, PropertyHelp> s_propertyHelp;

        /// <summary>
        ///     Help for MSBuild properties.
        /// </summary>
        private static readonly SortedDictionary<string, ItemHelp> s_itemHelp;

        /// <summary>
        ///     Help for MSBuild properties.
        /// </summary>
        private static readonly SortedDictionary<string, string> s_globalItemMetadataHelp;

        /// <summary>
        ///     Help for MSBuild tasks.
        /// </summary>
        private static readonly SortedDictionary<string, TaskHelp> s_taskHelp;

        /// <summary>
        ///     The names of well-known MSBuild properties.
        /// </summary>
        public static IEnumerable<string> WellKnownPropertyNames => s_propertyHelp.Keys;

        /// <summary>
        ///     The names of well-known MSBuild item types.
        /// </summary>
        public static IEnumerable<string> WellKnownItemTypes => s_itemHelp.Keys.Where(key => !key.Contains('*'));

        /// <summary>
        ///     The names of well-known metadata for the specified item type.
        /// </summary>
        /// <param name="itemType">
        ///     The item type, or "*" for global item metadata.
        /// </param>
        /// <param name="includeGlobals">
        ///     Include global (built-in) metadata names?
        /// </param>
        /// <returns>
        ///     A sequence of metadata names.
        /// </returns>
        public static IEnumerable<string> WellKnownItemMetadataNames(string itemType, bool includeGlobals = true)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'itemType'.", nameof(itemType));

            if (!s_itemHelp.TryGetValue(itemType, out ItemHelp itemHelp))
                yield break;

            // Well-known metadata.
            if (includeGlobals)
            {
                foreach (string metadataName in Utilities.MSBuildHelper.WellknownMetadataNames)
                    yield return metadataName;
            }

            // Item-type-specific metadata.
            foreach (string metadataName in itemHelp.Metadata.Keys)
                yield return metadataName;
        }

        /// <summary>
        ///     Get help content for the specified element.
        /// </summary>
        /// <param name="elementName">
        ///     The element name.
        /// </param>
        /// <returns>
        ///     The element help content, or <c>null</c> if no content is available for it.
        /// </returns>
        public static string ForElement(string elementName)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'elementName'.", nameof(elementName));

            string helpKey = elementName;

            // Prefer new help content (includes links to documentation).
            if (s_elementHelp.TryGetValue(helpKey, out ElementHelp elementHelp))
                return elementHelp.Description;

            // Fall back to existing help content.
            if (s_root.TryGetValue(helpKey, out string help))
                return help;

            return null;
        }

        /// <summary>
        ///     Get a help link (if available) for the specified element.
        /// </summary>
        /// <param name="elementName">
        ///     The element name.
        /// </param>
        /// <returns>
        ///     The element help link, or <c>null</c> if no link is available for it.
        /// </returns>
        public static string HelpLinkForElement(string elementName)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'elementName'.", nameof(elementName));

            string helpKey = elementName;
            if (s_elementHelp.TryGetValue(helpKey, out ElementHelp help))
                return help.HelpLink;

            return null;
        }

        /// <summary>
        ///     Get a help link (if available) for the specified item.
        /// </summary>
        /// <param name="itemType">
        ///     The item type name.
        /// </param>
        /// <returns>
        ///     The item help link, or <c>null</c> if no link is available for it.
        /// </returns>
        public static string HelpLinkForItem(string itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'itemName'.", nameof(itemType));

            string helpKey = itemType;
            if (s_itemHelp.TryGetValue(helpKey, out ItemHelp help))
                return help.HelpLink;

            return null;
        }

        /// <summary>
        ///     Get a help link (if available) for the specified property.
        /// </summary>
        /// <param name="propertyName">
        ///     The property name.
        /// </param>
        /// <returns>
        ///     The property help link, or <c>null</c> if no link is available for it.
        /// </returns>
        public static string HelpLinkForProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'propertyName'.", nameof(propertyName));

            string helpKey = propertyName;
            if (s_propertyHelp.TryGetValue(helpKey, out PropertyHelp help))
                return help.HelpLink;

            return null;
        }

        /// <summary>
        ///     Get help content for the specified attribute.
        /// </summary>
        /// <param name="elementName">
        ///     The element name.
        /// </param>
        /// <param name="attributeName">
        ///     The attribute name.
        /// </param>
        /// <returns>
        ///     The attribute help content, or <c>null</c> if no content is available for it.
        /// </returns>
        public static string ForAttribute(string elementName, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'elementName'.", nameof(elementName));

            string helpKey = string.Format("{0}.{1}", elementName, attributeName);
            if (s_root.TryGetValue(helpKey, out string help))
                return help;

            helpKey = string.Format("*.{0}", attributeName);
            if (s_root.TryGetValue(helpKey, out help))
                return help;

            return null;
        }

        /// <summary>
        ///     Get help content for a well-known MSBuild property.
        /// </summary>
        /// <param name="propertyName">
        ///     The property name.
        /// </param>
        /// <returns>
        ///     The property help content, or <c>null</c> if no content is available for it.
        /// </returns>
        public static string ForProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'propertyName'.", nameof(propertyName));

            string helpKey = propertyName;
            if (s_propertyHelp.TryGetValue(helpKey, out PropertyHelp help))
                return help.Description;

            return null;
        }

        /// <summary>
        ///     Get default value(s) for a well-known MSBuild property.
        /// </summary>
        /// <param name="propertyName">
        ///     The property name.
        /// </param>
        /// <returns>
        ///     A list containing the property's default values, or null if no defaults are available for it.
        /// </returns>
        public static IReadOnlyList<string> DefaultsForProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'propertyName'.", nameof(propertyName));

            if (s_propertyHelp.TryGetValue(propertyName, out PropertyHelp help))
                return help.DefaultValues;

            return null;
        }

        /// <summary>
        ///     Get help content for a well-known MSBuild item type.
        /// </summary>
        /// <param name="itemType">
        ///     The item type (e.g. Compile, Content).
        /// </param>
        /// <returns>
        ///     The item help content, or <c>null</c> if no content is available for it.
        /// </returns>
        public static string ForItemType(string itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'itemName'.", nameof(itemType));

            if (s_itemHelp.TryGetValue(itemType, out ItemHelp help))
                return help.Description;

            return null;
        }

        /// <summary>
        ///     Get help content for a well-known MSBuild item type's metadata.
        /// </summary>
        /// <param name="itemType">
        ///     The item type (e.g. Compile, Content).
        /// </param>
        /// <param name="metadataName">
        ///     The name of the item metadata.
        /// </param>
        /// <returns>
        ///     The item metadata help content, or <c>null</c> if no content is available for it.
        /// </returns>
        public static string ForItemMetadata(string itemType, string metadataName)
        {
            if (string.IsNullOrWhiteSpace(metadataName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'metadataName'.", nameof(metadataName));

            if (s_itemHelp.TryGetValue(itemType, out ItemHelp itemHelp) && itemHelp.Metadata.TryGetValue(metadataName, out string metadataHelp))
                return metadataHelp;

            if (s_globalItemMetadataHelp.TryGetValue(metadataName, out string globalMetadataHelp))
                return globalMetadataHelp;

            return null;
        }

        /// <summary>
        ///     Get help content for a well-known MSBuild task.
        /// </summary>
        /// <param name="taskName">
        ///     The task name.
        /// </param>
        /// <returns>
        ///     The task help content, or <c>null</c> if no content is available for it.
        /// </returns>
        public static string ForTask(string taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'itemName'.", nameof(taskName));

            if (s_taskHelp.TryGetValue(taskName, out TaskHelp taskHelp))
                return taskHelp.Description;

            return null;
        }

        /// <summary>
        ///     Get help content for a well-known MSBuild task's parameter.
        /// </summary>
        /// <param name="taskName">
        ///     The task name.
        /// </param>
        /// <param name="parameterName">
        ///     The parameter name.
        /// </param>
        /// <returns>
        ///     The parameter help content, or <c>null</c> if no content is available for it.
        /// </returns>
        public static string ForTaskParameter(string taskName, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'metadataName'.", nameof(parameterName));

            if (s_taskHelp.TryGetValue(taskName, out TaskHelp taskHelp) && taskHelp.Parameters.TryGetValue(parameterName, out TaskParameterHelp parameterHelp))
                return parameterHelp.Description;

            return null;
        }

        /// <summary>
        ///     Help content for root elements and attributes, keyed by "Element" or "Element.Attribute".
        /// </summary>
        /// <remarks>
        ///     Extracted from MSBuild.*.xsd - TODO: Move this to JSON-based help, same as property, item, and task help.
        /// </remarks>
        private static readonly Dictionary<string, string> s_root = new Dictionary<string, string>
        {
            ["*.Condition"] = "Optional expression evaluated to determine whether the target element should be evaluated",
            ["*.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["Choose"] = "Groups When and Otherwise elements",
            ["Choose.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["GenericProperty.Condition"] = "Optional expression evaluated to determine whether the property should be evaluated",
            ["GenericProperty.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["Import"] = "Declares that the contents of another project file should be inserted at this location",
            ["Import.Condition"] = "Optional expression evaluated to determine whether the import should occur",
            ["Import.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["Import.MinimumVersion"] = "Optional expression used to specify the minimum SDK version required by the referring import",
            ["Import.Project"] = "Project file to import",
            ["Import.Sdk"] = "Name of the SDK which contains the project file to import",
            ["Import.Version"] = "Optional expression used to specify the version of the SDK referenced by this import",
            ["ImportGroup"] = "Groups import definitions",
            ["ImportGroup.Condition"] = "Optional expression evaluated to determine whether the ImportGroup should be used",
            ["ImportGroup.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["ItemDefinitionGroup"] = "Groups item metadata definitions",
            ["ItemDefinitionGroup.Condition"] = "Optional expression evaluated to determine whether the ItemDefinitionGroup should be used",
            ["ItemDefinitionGroup.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["ItemGroup"] = "Groups item list definitions",
            ["ItemGroup.Condition"] = "Optional expression evaluated to determine whether the ItemGroup should be used",
            ["ItemGroup.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["OnError"] = "Specifies targets to execute in the event of a recoverable error",
            ["OnError.Condition"] = "Optional expression evaluated to determine whether the targets should be executed",
            ["OnError.ExecuteTargets"] = "Semicolon separated list of targets to execute",
            ["OnError.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["Otherwise"] = "Groups PropertyGroup and/or ItemGroup elements that are used if no Conditions on sibling When elements evaluate to true",
            ["ParameterGroup"] = "Groups parameters that are part of an inline task definition.",
            ["Project"] = "An MSBuild Project",
            ["Project.DefaultTargets"] = "Optional semi-colon separated list of one or more targets that will be built if no targets are otherwise specified",
            ["Project.InitialTargets"] = "Optional semi-colon separated list of targets that should always be built before any other targets",
            ["Project.Sdk"] = "Optional string describing the MSBuild SDK(s) this project should be built with",
            ["Project.ToolsVersion"] = "Optional string describing the toolset version this project should normally be built with",
            ["ProjectExtensions"] = "Optional section used by MSBuild hosts, that may contain arbitrary XML content that is ignored by MSBuild itself",
            ["PropertyGroup"] = "Groups property definitions",
            ["PropertyGroup.Condition"] = "Optional expression evaluated to determine whether the PropertyGroup should be used",
            ["PropertyGroup.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["SimpleItem.Condition"] = "Optional expression evaluated to determine whether the items should be evaluated",
            ["SimpleItem.Exclude"] = "Semicolon separated list of files (wildcards are allowed) or other item names to exclude from the Include list",
            ["SimpleItem.Include"] = "Semicolon separated list of files (wildcards are allowed) or other item names to include in this item list",
            ["SimpleItem.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["SimpleItem.Remove"] = "Semicolon separated list of files (wildcards are allowed) or other item names to remove from the existing list contents",
            ["SimpleItem.Update"] = "Semicolon separated list of files (wildcards are allowed) or other item names to be updated with the metadata from contained in this xml element",
            ["StringProperty.Condition"] = "Optional expression evaluated to determine whether the property should be evaluated",
            ["StringProperty.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["Target"] = "Groups tasks into a section of the build process",
            ["Target.AfterTargets"] = "Optional semi-colon separated list of targets that this target should run after.",
            ["Target.BeforeTargets"] = "Optional semi-colon separated list of targets that this target should run before.",
            ["Target.Condition"] = "Optional expression evaluated to determine whether the Target and the targets it depends on should be run",
            ["Target.DependsOnTargets"] = "Optional semi-colon separated list of targets that should be run before this target",
            ["Target.Inputs"] = "Optional semi-colon separated list of files that form inputs into this target. Their timestamps will be compared with the timestamps of files in Outputs to determine whether the Target is up to date",
            ["Target.KeepDuplicateOutputs"] = "Optional expression evaluated to determine whether duplicate items in the Target's Returns should be removed before returning them. The default is not to eliminate duplicates.",
            ["Target.Label"] = "Optional expression. Used to identify or order system and user elements",
            ["Target.Name"] = "Name of the target",
            ["Target.Outputs"] = "Optional semi-colon separated list of files that form outputs into this target. Their timestamps will be compared with the timestamps of files in Inputs to determine whether the Target is up to date",
            ["Target.Returns"] = "Optional expression evaluated to determine which items generated by the target should be returned by the target. If there are no Returns attributes on Targets in the file, the Outputs attributes are used instead for this purpose.",
            ["Task.Architecture"] = "Defines the bitness of the task if it must be run specifically in a 32bit or 64bit process. If not specified, it will run with the bitness of the build process.  If there are multiple tasks defined in UsingTask with the same name but with different Architecture attribute values, the value of the Architecture attribute specified here will be used to match and select the correct task",
            ["Task.Condition"] = "Optional expression evaluated to determine whether the task should be executed",
            ["Task.ContinueOnError"] = "Optional boolean indicating whether a recoverable task error should be ignored. Default false",
            ["Task.Output"] = "Optional element specifying a specific task output to be gathered",
            ["Task.Output.Condition"] = "Optional expression evaluated to determine whether the output should be gathered",
            ["Task.Output.ItemName"] = "Optional name of an item list to put the gathered outputs into. Either ItemName or PropertyName must be specified",
            ["Task.Output.PropertyName"] = "Optional name of a property to put the gathered output into. Either PropertyName or ItemName must be specified",
            ["Task.Output.TaskParameter"] = "Task parameter to gather. Matches the name of a .NET Property on the task class that has an [Output] attribute",
            ["Task.Runtime"] = "Defines the .NET runtime of the task. This must be specified if the task must run on a specific version of the .NET runtime. If not specified, the task will run on the runtime being used by the build process. If there are multiple tasks defined in UsingTask with the same name but with different Runtime attribute values, the value of the Runtime attribute specified here will be used to match and select the correct task",
            ["UsingTask"] = "Defines the assembly containing a task's implementation, or contains the implementation itself.",
            ["UsingTask.Architecture"] = "Defines the architecture of the task host that this task should be run in.  Currently supported values:  x86, x64, CurrentArchitecture, and * (any).  If Architecture is not specified, either the task will be run within the MSBuild process, or the task host will be launched using the architecture of the parent MSBuild process",
            ["UsingTask.AssemblyFile"] = "Optional path to assembly containing the task. Either AssemblyName or AssemblyFile must be used",
            ["UsingTask.AssemblyName"] = "Optional name of assembly containing the task. Either AssemblyName or AssemblyFile must be used",
            ["UsingTask.Condition"] = "Optional expression evaluated to determine whether the declaration should be evaluated",
            ["UsingTask.Runtime"] = "Defines the .NET runtime version of the task host that this task should be run in.  Currently supported values:  CLR2, CLR4, CurrentRuntime, and * (any).  If Runtime is not specified, either the task will be run within the MSBuild process, or the task host will be launched using the runtime of the parent MSBuild process",
            ["UsingTask.TaskFactory"] = "Name of the task factory class in the assembly",
            ["UsingTask.TaskName"] = "Name of task class in the assembly",
            ["UsingTaskBody"] = "Contains the inline task implementation. Content is opaque to MSBuild.",
            ["UsingTaskBody.Evaluate"] = "Whether the body should have properties expanded before use. Defaults to false.",
            ["When"] = "Groups PropertyGroup and/or ItemGroup elements",
            ["When.Condition"] = "Optional expression evaluated to determine whether the child PropertyGroups and/or ItemGroups should be used",
        };
    }
}
