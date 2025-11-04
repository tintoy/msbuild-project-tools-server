using System;
using Superpower;
using Superpower.Model;

namespace MSBuildProjectTools.LanguageServer.SemanticModel.MSBuildExpressions
{
    using Position = Superpower.Model.Position;
    
    /// <summary>
    /// An interface for objects that have a source <see cref="Position"/>.
    /// </summary>
    /// <typeparam name="T">Type of the matched result.</typeparam>
    public interface IPositionAware<out T>
    {
        /// <summary>
        /// Set the start <see cref="Position"/> and the matched length.
        /// </summary>
        /// <param name="startPos">The start position</param>
        /// <param name="length">The matched length.</param>
        /// <returns>The matched result.</returns>
        T SetPos(Position startPos, int length);

        /// <summary>
        /// Set the <see cref="Position"/> and length from matched <see cref="TextSpan"/>.
        /// </summary>
        /// <param name="span">The matched span of text.</param>
        /// <returns>The matched result.</returns>
        T SetPos(TextSpan span) => SetPos(span.Position, span.Length);
    }
}
