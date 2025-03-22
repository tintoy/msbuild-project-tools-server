using Microsoft.VisualStudio.SolutionPersistence.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    using System.Formats.Asn1;
    using Utilities;

    /// <summary>
    ///     A facility for looking up Visual Studio Solution members by textual location.
    /// </summary>
    public class VSSolutionObjectLocator
    {
        /// <summary>
        ///     The ranges for all XML objects in the document with positional annotations.
        /// </summary>
        /// <remarks>
        ///     Sorted by range comparison (effectively, this means document order).
        /// </remarks>
        readonly List<Range> _objectRanges = new List<Range>();

        /// <summary>
        ///     All objects in the project, keyed by starting position.
        /// </summary>
        /// <remarks>
        ///     Sorted by range comparison.
        /// </remarks>
        readonly SortedDictionary<Position, VSSolutionObject> _objectsByStartPosition = new SortedDictionary<Position, VSSolutionObject>();

        /// <summary>
        ///     The Visual Studio Solution.
        /// </summary>
        readonly SolutionModel _solutionModel;

        /// <summary>
        ///     The solution XML.
        /// </summary>
        readonly XmlLocator _solutionXml;

        /// <summary>
        ///     The position-lookup for the project XML.
        /// </summary>
        readonly TextPositions _solutionXmlPositions;

        /// <summary>
        ///     Create a new <see cref="VSSolutionObjectLocator"/>.
        /// </summary>
        /// <param name="solutionModel">
        ///     The deserialised Visual Studio Solution model.
        /// </param>
        /// <param name="solutionXml">
        ///     The <see cref="XmlLocator"/> for the project XML.
        /// </param>
        /// <param name="solutionXmlPositions">
        ///     The position-lookup for the project XML.
        /// </param>
        public VSSolutionObjectLocator(SolutionModel solutionModel, XmlLocator solutionXml, TextPositions solutionXmlPositions)
        {
            if (solutionModel == null)
                throw new ArgumentNullException(nameof(solutionModel));

            if (solutionXml == null)
                throw new ArgumentNullException(nameof(solutionXml));

            if (solutionXmlPositions == null)
                throw new ArgumentNullException(nameof(solutionXmlPositions));

            _solutionModel = solutionModel;
            _solutionXml = solutionXml;
            _solutionXmlPositions = solutionXmlPositions;

            // TODO: Process solution model.

            _objectRanges.Sort();
        }

        /// <summary>
        ///     All known Visual Studio Solution objects.
        /// </summary>
        public IEnumerable<VSSolutionObject> AllObjects => _objectsByStartPosition.Values;

        /// <summary>
        ///     Find the project object (if any) at the specified position.
        /// </summary>
        /// <param name="position">
        ///     The target position .
        /// </param>
        /// <returns>
        ///     The project object, or <c>null</c> if no object was found at the specified position.
        /// </returns>
        public VSSolutionObject Find(Position position)
        {
            // Internally, we always use 1-based indexing because this is what the System.Xml APIs use (and I'd rather keep things simple).
            position = position.ToOneBased();

            // Short-circuit.
            if (_objectsByStartPosition.TryGetValue(position, out VSSolutionObject exactMatch))
                return exactMatch;

            // TODO: Use binary search.

            Range lastMatchingRange = Range.Zero;
            foreach (Range objectRange in _objectRanges)
            {
                if (lastMatchingRange != Range.Zero && objectRange.End > lastMatchingRange.End)
                    break; // We've moved past the end of the last matching range.

                if (objectRange.Contains(position))
                    lastMatchingRange = objectRange;
            }
            if (lastMatchingRange == Range.Zero)
                return null;

            return _objectsByStartPosition[lastMatchingRange.Start];
        }

        /// <summary>
        ///     Add the Visual Studio Solution object to the locator.
        /// </summary>
        /// <param name="vsSolutionObject">
        ///     The <see cref="VSSolutionObject"/>.
        /// </param>
        void Add(VSSolutionObject vsSolutionObject)
        {
            if (vsSolutionObject == null)
                throw new ArgumentNullException(nameof(vsSolutionObject));

            if (_objectsByStartPosition.TryGetValue(vsSolutionObject.XmlRange.Start, out VSSolutionObject dupe))
            {
                Serilog.Log.Information("Found duplicate {0} (vs {1}) at {2} (vs {3}). Same underlying object: {IdentityMatch}",
                    vsSolutionObject.Kind,
                    dupe.Kind,
                    vsSolutionObject.XmlRange,
                    dupe.XmlRange,
                    vsSolutionObject.IsSameUnderlyingObject(dupe)
                );
            }

            _objectRanges.Add(vsSolutionObject.XmlRange);
            _objectsByStartPosition.Add(vsSolutionObject.XmlRange.Start, vsSolutionObject);
        }
    }
}
