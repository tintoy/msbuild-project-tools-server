using System;
using System.Collections.Generic;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     A quick-and-dirty calculator for text positions.
    /// </summary>
    /// <remarks>
    ///     This could easily be improved by also storing a character sub-total for each line.
    /// </remarks>
    public sealed class TextPositions
    {
        /// <summary>
        ///     The absolution starting position, within the text, of each line.
        /// </summary>
        private readonly int[] _lineStartPositions;

        /// <summary>
        ///     Create a new <see cref="TextPositions"/> for the specified text.
        /// </summary>
        /// <param name="text">
        ///     The text.
        /// </param>
        public TextPositions(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _lineStartPositions = CalculateLineStartPositions(text);
        }

        /// <summary>
        ///     The number of lines in the text.
        /// </summary>
        public int LineCount => _lineStartPositions.Length;

        /// <summary>
        ///     The absolution starting position, within the text, of each line.
        /// </summary>
        public IReadOnlyList<int> LineStartPositions => _lineStartPositions;

        /// <summary>
        ///     Convert a <see cref="Position"/> to an absolute position within the text.
        /// </summary>
        /// <param name="position">
        ///     The target <see cref="Position"/> (0-based or 1-based).
        /// </param>
        /// <returns>
        ///     The equivalent absolute position (0-based) within the text.
        /// </returns>
        public int GetAbsolutePosition(Position position)
        {
            position = position.ToZeroBased();

            return GetAbsolutePosition(position.LineNumber, position.ColumnNumber);
        }

        /// <summary>
        ///     Convert line and column numbers to an absolute position within the text.
        /// </summary>
        /// <param name="line">
        ///     The target line (0-based).
        /// </param>
        /// <param name="column">
        ///     The target column (0-based).
        /// </param>
        /// <returns>
        ///     The equivalent absolute position within the text.
        /// </returns>
        public int GetAbsolutePosition(int line, int column)
        {
            if (line < 0)
                throw new ArgumentOutOfRangeException(nameof(line), line, "Line cannot be less than 0.");

            if (line >= _lineStartPositions.Length)
                throw new ArgumentOutOfRangeException(nameof(line), line, "Line is past the end of the text.");

            if (column < 0)
                throw new ArgumentOutOfRangeException(nameof(column), column, "Column cannot be less than 0.");

            return _lineStartPositions[line] + column;
        }

        /// <summary>
        ///     Convert an absolute position to a line and column in the text.
        /// </summary>
        /// <param name="absolutePosition">
        ///     The absolute position (0-based).
        /// </param>
        /// <returns>
        ///     The equivalent <see cref="Position"/> within the text.
        /// </returns>
        public Position GetPosition(int absolutePosition)
        {
            int targetLine = Array.BinarySearch(_lineStartPositions, absolutePosition);
            if (targetLine < 0)
                targetLine = ~targetLine - 1; // No match, so BinarySearch returns 2's complement of the following line index.

            // Internally, we're 0-based, but lines and columns are (by convention) 1-based.
            return Position.FromZeroBased(
                lineNumber: targetLine,
                columnNumber: absolutePosition - _lineStartPositions[targetLine]
            ).ToOneBased();
        }

        /// <summary>
        ///     Get a <see cref="Range"/> representing the specified absolute positions.
        /// </summary>
        /// <param name="absoluteStartPosition">
        ///     The (0-based) absolute start position.
        /// </param>
        /// <param name="absoluteEndPosition">
        ///     The (1-based) absolute end position.
        /// </param>
        /// <returns>
        ///     The <see cref="Range"/>.
        /// </returns>
        public Range GetRange(int absoluteStartPosition, int absoluteEndPosition)
        {
            return new Range(
                start: GetPosition(absoluteStartPosition),
                end: GetPosition(absoluteEndPosition)
            );
        }

        /// <summary>
        ///     Calculate the length of the specified <see cref="Range"/> in the text.
        /// </summary>
        /// <param name="range">
        ///     The range.
        /// </param>
        /// <returns>
        ///     The range length.
        /// </returns>
        public int GetLength(Range range)
        {
            return GetDistance(range.Start, range.End);
        }

        /// <summary>
        ///     Calculate the number of characters, in the text, between the specified positions.
        /// </summary>
        /// <param name="position1">
        ///     The first position.
        /// </param>
        /// <param name="position2">
        ///     The second position.
        /// </param>
        /// <returns>
        ///     The difference in offset between <paramref name="position2"/> and <paramref name="position1"/> (can be negative).
        /// </returns>
        public int GetDistance(Position position1, Position position2)
        {
            return GetAbsolutePosition(position2) - GetAbsolutePosition(position1);
        }

        /// <summary>
        ///     Move the specified position closer to the start of the document by the specified number of characters.
        /// </summary>
        /// <param name="position">
        ///     The position.
        /// </param>
        /// <param name="byCharCount">
        ///     The number of characters.
        /// </param>
        /// <returns>
        ///     The updated position. If <paramref name="byCharCount"/> would take the position past the start of the document, this becomes (1,1).
        /// </returns>
        public Position MoveLeft(Position position, int byCharCount)
        {
            int absolutePosition = GetAbsolutePosition(position);
            absolutePosition -= byCharCount;
            if (absolutePosition < 0)
                absolutePosition = 0;

            return GetPosition(absolutePosition);
        }

        /// <summary>
        ///     Extend the specified range closer to the start of the document by the specified number of characters.
        /// </summary>
        /// <param name="range">
        ///     The range.
        /// </param>
        /// <param name="byCharCount">
        ///     The number of characters.
        /// </param>
        /// <returns>
        ///     The updated range. If <paramref name="byCharCount"/> would take the range start past the start of the document, this becomes (1,1).
        /// </returns>
        public Range ExtendLeft(Range range, int byCharCount)
        {
            return range.WithStart(
                MoveLeft(range.Start, byCharCount)
            );
        }

        /// <summary>
        ///     Calculate the start position for each line in the text.
        /// </summary>
        /// <param name="text">
        ///     The text to scan.
        /// </param>
        /// <returns>
        ///     An array of line starting positions.
        /// </returns>
        static int[] CalculateLineStartPositions(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            List<int> lineStarts = new List<int>();

            int currentPosition = 0;
            int currentLineStart = 0;
            while (currentPosition < text.Length)
            {
                char currentChar = text[currentPosition];
                currentPosition++;

                switch (currentChar)
                {
                    case '\r':
                    {
                        if (currentPosition < text.Length && text[currentPosition] == '\n')
                            currentPosition++;

                        goto case '\n';
                    }
                    case '\n':
                    {
                        lineStarts.Add(currentLineStart);
                        currentLineStart = currentPosition;

                        break;
                    }
                }
            }
            lineStarts.Add(currentLineStart);

            return lineStarts.ToArray();
        }
    }
}
