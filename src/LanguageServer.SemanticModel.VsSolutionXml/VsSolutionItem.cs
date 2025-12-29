using Microsoft.VisualStudio.SolutionPersistence.Model;
using MSBuildProjectTools.LanguageServer.Utilities;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents an item in a solution's semantic model.
    /// </summary>
    public class VsSolutionItem(VsSolution solution, SolutionItemModel solutionProject, XSNode declaringXml)
        : VsSolutionObject<SolutionItemModel>(solution, solutionProject, declaringXml)
    {
        /// <summary>
        ///     The underlying <see cref="SolutionItemModel"/> represented by the <see cref="VsSolutionItem"/>.
        /// </summary>
        public SolutionItemModel Item => UnderlyingObject;

        /// <summary>
        ///     The object's name.
        /// </summary>
        public override string Name => Item.ActualDisplayName;

        /// <summary>
        ///     The kind of solution object represented by the <see cref="VsSolutionItem"/>.
        /// </summary>
        public override VsSolutionObjectKind Kind => VsSolutionObjectKind.Item;

        /// <summary>
        ///     The full path of the file where the <see cref="VsSolutionItem"/> is declared.
        /// </summary>
        public override string SourceFile => Solution.File.FullName;
    }
}
