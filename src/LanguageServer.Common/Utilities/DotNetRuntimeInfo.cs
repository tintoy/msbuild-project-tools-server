using System;
using System.Diagnostics;
using System.IO;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Information about the .NET Core runtime.
    /// </summary>
    public class DotNetRuntimeInfo
    {
        /// <summary>
        ///     The .NET Core version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        ///     The .NET Core base directory.
        /// </summary>
        public string BaseDirectory { get; set; }

        /// <summary>
        ///     The current runtime identifier (RID).
        /// </summary>
        public string RID { get; set; }

        /// <summary>
        ///     Get information about the current .NET Core runtime.
        /// </summary>
        /// <param name="baseDirectory">
        ///     An optional base directory where dotnet.exe should be run (this may affect the version it reports due to global.json).
        /// </param>
        /// <returns>
        ///     A <see cref="DotNetRuntimeInfo"/> containing the runtime information.
        /// </returns>
        public static DotNetRuntimeInfo GetCurrent(string baseDirectory = null)
        {
            DotNetRuntimeInfo runtimeInfo = new DotNetRuntimeInfo();

            Process dotnetInfoProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = baseDirectory,
                Arguments = "--info",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
            using (dotnetInfoProcess)
            {
                dotnetInfoProcess.WaitForExit();

                return ParseDotNetInfoOutput(dotnetInfoProcess.StandardOutput);
            }
        }

        /// <summary>
        ///     Parse the output of "dotnet --info" into a <see cref="DotNetRuntimeInfo"/>.
        /// </summary>
        /// <param name="dotnetInfoOutput">
        ///     A <see cref="TextReader"/> containing the output of "dotnet --info".
        /// </param>
        /// <returns>
        ///     The <see cref="DotNetRuntimeInfo"/>.
        /// </returns>
        public static DotNetRuntimeInfo ParseDotNetInfoOutput(TextReader dotnetInfoOutput)
        {
            if (dotnetInfoOutput == null)
                throw new ArgumentNullException(nameof(dotnetInfoOutput));

            DotNetRuntimeInfo runtimeInfo = new DotNetRuntimeInfo();

            DotnetInfoSection currentSection = DotnetInfoSection.Start;

            string currentLine;
            while ((currentLine = dotnetInfoOutput.ReadLine()) != null)
            {
                if (String.IsNullOrWhiteSpace(currentLine))
                    continue;

                if (!currentLine.StartsWith(" ") && currentLine.EndsWith(":"))
                {
                    currentSection++;

                    if (currentSection > DotnetInfoSection.RuntimeEnvironment)
                        break;

                    continue;
                }

                string[] property = currentLine.Split(new char[] { ':' }, count: 2);
                if (property.Length != 2)
                    continue;

                property[0] = property[0].Trim();
                property[1] = property[1].Trim();

                switch (currentSection)
                {
                    case DotnetInfoSection.ProductInformation:
                    {
                        switch (property[0])
                        {
                            case "Version":
                            {
                                runtimeInfo.Version = property[1];

                                break;
                            }
                        }

                        break;
                    }
                    case DotnetInfoSection.RuntimeEnvironment:
                    {
                        switch (property[0])
                        {
                            case "Base Path":
                            {
                                runtimeInfo.BaseDirectory = property[1];

                                break;
                            }
                            case "RID":
                            {
                                runtimeInfo.RID = property[1];

                                break;
                            }
                        }

                        break;
                    }
                }
            }

            return runtimeInfo;
        }
    }

    /// <summary>
    ///     Well-known sections returned by "dotnet --info".
    /// </summary>
    /// <remarks>
    ///     Since the section titles returned by "dotnet --info" are now localised, we have to resort to this (more-fragile) method of parsing the output.
    /// </remarks>
    enum DotnetInfoSection
    {
        /// <summary>
        ///     Start of output.
        /// </summary>
        Start = 0,

        /// <summary>
        ///     The product information section (e.g. ".NET Core SDK (reflecting any global.json)").
        /// </summary>
        ProductInformation = 1,

        /// <summary>
        ///     The runtime environment section (e.g. "Runtime Environment").
        /// </summary>
        RuntimeEnvironment = 2,

        /// <summary>
        ///     The host section (e.g. "Host (useful for support)").
        /// </summary>
        Host = 3,

        /// <summary>
        ///     The SDK list section (e.g. ".NET Core SDKs installed").
        /// </summary>
        SdkList = 4,

        /// <summary>
        ///     The runtime list section (e.g. ".NET Core runtimes installed").
        /// </summary>
        RuntimeList = 5
    }
}
