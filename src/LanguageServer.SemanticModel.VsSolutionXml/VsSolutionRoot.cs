using Microsoft.VisualStudio.SolutionPersistence.Model;
using MSBuildProjectTools.LanguageServer.Utilities;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents the root of a solution's semantic model.
    /// </summary>
    public class VsSolutionRoot(VsSolution solution, SolutionModel solutionRoot, XSNode declaringXml)
        : VsSolutionObject<SolutionModel>(solution, solutionRoot, declaringXml)
    {
        /// <summary>
        ///     The object's name.
        /// </summary>
        public override string Name => Solution.File.Name;

        /// <summary>
        ///     The kind of solution object represented by the <see cref="VsSolutionRoot"/>.
        /// </summary>
        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.Solution;

        /// <summary>
        ///     The full path of the file where the <see cref="VsSolutionRoot"/> is declared.
        /// </summary>
        public override string SourceFile => Solution.File.FullName;
    }
}
