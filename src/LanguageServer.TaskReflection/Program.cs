using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace MSBuildProjectTools.LanguageServer.TaskReflection
{
    using LanguageServer.Utilities;
    using System.Runtime.Loader;

    /// <summary>
    ///     A tool to scan an MSBuild task assembly and output information about the tasks it contains.
    /// </summary>
    public static class Program
    {
        /// <summary>
        ///     The fully-qualified names of supported task parameter types.
        /// </summary>
        private static readonly HashSet<string> s_supportedTaskParameterTypes = new HashSet<string>
        {
            typeof(string).FullName,
            typeof(bool).FullName,
            typeof(char).FullName,
            typeof(byte).FullName,
            typeof(short).FullName,
            typeof(int).FullName,
            typeof(long).FullName,
            typeof(float).FullName,
            typeof(double).FullName,
            typeof(DateTime).FullName,
            typeof(Guid).FullName,
            "Microsoft.Build.Framework.ITaskItem",
            "Microsoft.Build.Framework.ITaskItem[]",
            "Microsoft.Build.Framework.ITaskItem2",
            "Microsoft.Build.Framework.ITaskItem2[]"
        };

        /// <summary>
        ///     The main program entry-point.
        /// </summary>
        /// <param name="args">
        ///     Command-line arguments.
        /// </param>
        /// <returns>
        ///     0 if successful; otherwise, 1.
        /// </returns>
        public static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                WriteErrorJson("Must specify the task assembly file to examine.");

                return 1;
            }

            try
            {
                // TODO: Consider looking for IntelliDoc XML file to resolve help for tasks and task parameters.

                FileInfo tasksAssemblyFile = new FileInfo(args[0]);
                if (!tasksAssemblyFile.Exists)
                {
                    WriteErrorJson("Cannot find file '{0}'.", tasksAssemblyFile.FullName);

                    return 1;
                }

                DotNetRuntimeInfo runtimeInfo = DotNetRuntimeInfo.GetCurrent(tasksAssemblyFile.Directory.FullName);

                string fallbackDirectory = runtimeInfo.BaseDirectory;
                string baseDirectory = tasksAssemblyFile.DirectoryName;
                AssemblyLoadContext fallbackAssemblyLoadContext = AssemblyLoadContext.Default;

                DirectoryAssemblyLoadContext loadContext = new DirectoryAssemblyLoadContext(baseDirectory, fallbackDirectory, fallbackAssemblyLoadContext);

                Assembly tasksAssembly = loadContext.LoadFromAssemblyPath(tasksAssemblyFile.FullName);
                if (tasksAssembly == null)
                {
                    WriteErrorJson("Unable to load assembly '{0}'.", tasksAssemblyFile.FullName);

                    return 1;
                }

                Type[] taskTypes;
                try
                {
                    taskTypes = tasksAssembly.GetTypes();
                }
                catch (ReflectionTypeLoadException typeLoadError)
                {
                    taskTypes = typeLoadError.Types;
                }

                taskTypes =
                    taskTypes.Where(type =>
                        type != null // Type could not be loaded (see typeLoadError.LoaderExceptions above)
                        &&
                        !type.IsNested && type.IsClass && !type.IsAbstract && type.GetInterfaces().Any(interfaceType => interfaceType.FullName == "Microsoft.Build.Framework.ITask")
                    )
                    .ToArray();

                JsonWriterOptions jsonOptions = new JsonWriterOptions
                {
                    Indented = true,
                };

                MemoryStream memoryStream = new MemoryStream();
                using (Utf8JsonWriter jsonWriter = new Utf8JsonWriter(memoryStream, jsonOptions))
                {
                    jsonWriter.WriteStartObject();

                    jsonWriter.WriteString("assemblyName", tasksAssembly.FullName);
                    jsonWriter.WriteString("assemblyPath", tasksAssemblyFile.FullName);
                    jsonWriter.WriteString("timestampUtc", tasksAssemblyFile.LastWriteTimeUtc);

                    jsonWriter.WritePropertyName("tasks");
                    jsonWriter.WriteStartArray();

                    foreach (Type taskType in taskTypes)
                    {
                        jsonWriter.WriteStartObject();

                        jsonWriter.WriteString("taskName", taskType.Name);
                        jsonWriter.WriteString("typeName", taskType.FullName);

                        PropertyInfo[] properties =
                            taskType.GetProperties()
                                .Where(property =>
                                    (property.CanRead && property.GetGetMethod().IsPublic) ||
                                    (property.CanWrite && property.GetSetMethod().IsPublic)
                                )
                                .ToArray();

                        jsonWriter.WritePropertyName("parameters");
                        jsonWriter.WriteStartArray();

                        foreach (PropertyInfo property in properties)
                        {
                            if (!s_supportedTaskParameterTypes.Contains(property.PropertyType.FullName) && !s_supportedTaskParameterTypes.Contains(property.PropertyType.FullName + "[]") && !property.PropertyType.IsEnum)
                                continue;

                            jsonWriter.WriteStartObject();

                            jsonWriter.WriteString("parameterName", property.Name);
                            jsonWriter.WriteString("parameterType", property.PropertyType.FullName);

                            if (property.PropertyType.IsEnum)
                            {
                                jsonWriter.WritePropertyName("enum");
                                jsonWriter.WriteStartArray();

                                foreach (string enumMember in Enum.GetNames(property.PropertyType))
                                    jsonWriter.WriteStringValue(enumMember);

                                jsonWriter.WriteEndArray();
                            }

                            bool isRequired = property.GetCustomAttributes().Any(attribute => attribute.GetType().FullName == "Microsoft.Build.Framework.RequiredAttribute");
                            if (isRequired)
                                jsonWriter.WriteBoolean("required", true);

                            bool isOutput = property.GetCustomAttributes().Any(attribute => attribute.GetType().FullName == "Microsoft.Build.Framework.OutputAttribute");
                            if (isOutput)
                                jsonWriter.WriteBoolean("output", true);

                            jsonWriter.WriteEndObject();
                        }

                        jsonWriter.WriteEndArray();

                        jsonWriter.WriteEndObject();
                    }

                    jsonWriter.WriteEndArray();

                    jsonWriter.WriteEndObject();
                }

                memoryStream.Position = 0;
                StreamReader reader = new StreamReader(memoryStream);
                Console.WriteLine(reader.ReadToEnd());

                return 0;
            }
            catch (Exception unexpectedError)
            {
                System.Diagnostics.Debug.WriteLine(unexpectedError);

                WriteErrorJson(unexpectedError.ToString());

                return 1;
            }
            finally
            {
                Console.Out.Flush();
            }
        }

        /// <summary>
        ///     Write an error message in JSON format.
        /// </summary>
        /// <param name="messageOrFormat">
        ///     The error message or message-format specifier.
        /// </param>
        /// <param name="formatArgs">
        ///     Optional message-format arguments.
        /// </param>
        static void WriteErrorJson(string messageOrFormat, params object[] formatArgs)
        {
            string message = formatArgs.Length > 0 ? string.Format(messageOrFormat, formatArgs) : messageOrFormat;
            ErrorMessageModel messageModel = new ErrorMessageModel(message);

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            string json = JsonSerializer.Serialize(messageModel, jsonOptions);
            Console.WriteLine(json);
        }

        private class ErrorMessageModel
        {
            public string Message { get; init; }

            public ErrorMessageModel(string message)
            {
                Message = message;
            }
        }
    }
}
