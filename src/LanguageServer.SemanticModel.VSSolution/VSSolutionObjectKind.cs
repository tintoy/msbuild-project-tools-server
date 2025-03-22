using Microsoft.VisualStudio.SolutionPersistence.Model;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     A type of Visual Studio Solution object.
    /// </summary>
    public enum VSSolutionObjectKind
    {
        /// <summary>
        ///     An object in an invalid Visual Studio Solution.
        /// </summary>
        Invalid = -1,

        /// <summary>
        ///     An unknown Visual Studio Solution object.
        /// </summary>
        Unknown = 0,

        /// <summary>
        ///     A project (<see cref="SolutionProjectModel"/>) in a Visual Studio Solution.
        /// </summary>
        Project = 1,
    }
}
