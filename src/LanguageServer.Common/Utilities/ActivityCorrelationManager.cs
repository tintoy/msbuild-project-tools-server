using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;

namespace MSBuildProjectTools.LanguageServer.Utilities
{
    /// <summary>
    ///     Handles activity correlation in an <c>async</c>/<c>await</c>-friendly manner.
    /// </summary>
    public static class ActivityCorrelationManager
    {
        /// <summary>
        ///     The current activity id.
        /// </summary>
        private static readonly AsyncLocal<Guid?> s_currentActivityIdInternal = new AsyncLocal<Guid?>();

        /// <summary>
        ///     The System.Diagnostics correlation manager.
        /// </summary>
        private static readonly CorrelationManager s_systemCorrelationManager = Trace.CorrelationManager;

        /// <summary>
        ///     Get the current activity Id (if any).
        /// </summary>
        public static Guid? CurrentActivityId => s_currentActivityIdInternal.Value;

        /// <summary>
        ///     Create an activity scope.
        /// </summary>
        /// <param name="activityId">
        ///     An optional specific activity Id to use.
        ///     If not specified, a new activity Id is generated.
        /// </param>
        /// <returns>
        ///     The new activity scope.
        /// </returns>
        /// <remarks>
        ///     When the scope is disposed, the previous activity Id (if any) will be restored.
        /// </remarks>
        public static ActivityScope BeginActivityScope(Guid? activityId = null)
        {
            if (activityId == Guid.Empty)
            {
                throw new ArgumentException("GUID cannot be empty: 'activityId'.", nameof(activityId));
            }

            return new ActivityScope(activityId ?? Guid.NewGuid(), CurrentActivityId);
        }

        /// <summary>
        ///     Create an activity scope that ensures a that there is a current activity.
        /// </summary>
        /// <returns>
        ///     The new activity scope.
        /// </returns>
        /// <remarks>
        ///     If there is already an ambient activity, the scope will maintain it.
        ///     When the scope is disposed, the previous activity Id (if any) will be restored.
        /// </remarks>
        public static ActivityScope RequireActivityScope()
        {
            return new ActivityScope(CurrentActivityId ?? Guid.NewGuid(), CurrentActivityId);
        }

        /// <summary>
        ///     Create a new activity scope that suppresses the ambient activity.
        /// </summary>
        /// <returns>
        ///     The new activity scope.
        /// </returns>
        /// <remarks>
        ///     When the scope is disposed, the previous activity Id (if any) will be restored.
        /// </remarks>
        public static ActivityScope BeginSuppressActivityScope()
        {
            return new ActivityScope(null, CurrentActivityId);
        }

        /// <summary>
        ///     Set the current activity Id.
        /// </summary>
        /// <param name="activityId">
        ///     The activity Id.
        /// </param>
        internal static void SetCurrentActivityId(Guid activityId)
        {
            if (activityId == Guid.Empty)
            {
                throw new ArgumentException("GUID cannot be empty: 'activityId'.", nameof(activityId));
            }

            s_currentActivityIdInternal.Value = activityId;
        }

        /// <summary>
        ///     Clear the current activity Id.
        /// </summary>
        internal static void ClearCurrentActivityId()
        {
            s_currentActivityIdInternal.Value = null;
        }

        /// <summary>
        ///     Update event source activity Ids with the current activity Id (if one is currently set).
        /// </summary>
        /// <param name="correlationSource">
        ///     A <see cref="CorrelationSource" /> value representing the source of activity-correlation information.
        ///     Default is <see cref="CorrelationSource.Application" />.
        /// </param>
        internal static void SynchronizeEventSourceActivityIds(CorrelationSource correlationSource = CorrelationSource.Application)
        {
            switch (correlationSource)
            {
                case CorrelationSource.Application:
                {
                    var currentActivityId = CurrentActivityId ?? Guid.Empty;

                    if (EventSource.CurrentThreadActivityId != currentActivityId)
                    {
                        EventSource.SetCurrentThreadActivityId(currentActivityId);
                    }

                    if (s_systemCorrelationManager.ActivityId != currentActivityId)
                    {
                        s_systemCorrelationManager.ActivityId = currentActivityId;
                    }

                    break;
                }

                case CorrelationSource.EventSource:
                {
                    var currentActivityId = EventSource.CurrentThreadActivityId;

                    if (currentActivityId != Guid.Empty)
                    {
                        SetCurrentActivityId(currentActivityId);
                    }
                    else
                    {
                        ClearCurrentActivityId();
                    }

                    s_systemCorrelationManager.ActivityId = currentActivityId;

                    break;
                }

                default:
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(correlationSource),
                        $"Unsupported correlation source: '{correlationSource}'.");
                }
            }
        }
    }

    /// <summary>
    ///     Represents a source of activity correlation information.
    /// </summary>
    [Serializable]
    public enum CorrelationSource
    {
        /// <summary>
        ///     The source of activity-correlation information is unknown.
        /// </summary>
        /// <remarks>
        ///     Used to detect uninitialized values; do not use directly.
        /// </remarks>
        Unknown = 0,

        /// <summary>
        ///     Activity-correlation information comes from the application.
        /// </summary>
        Application = 1,

        /// <summary>
        ///     Activity-correlation information comes from <see cref="EventSource">ETW</see>'s <see cref="EventSource.CurrentThreadActivityId" />.
        /// </summary>
        EventSource = 2
    }

    /// <summary>
    ///     Represents a scope for an activity.
    /// </summary>
    /// <remarks>
    ///     When the scope is disposed, the previous activity Id (if any) will be restored.
    /// </remarks>
    /// <seealso cref="ActivityCorrelationManager" />
    public sealed class ActivityScope : IDisposable
    {
        /// <summary>
        ///     The current activity Id (if any).
        /// </summary>
        private readonly Guid? _activityId;

        /// <summary>
        ///     The previous activity Id (if any).
        /// </summary>
        private readonly Guid? _previousActivityId;

        /// <summary>
        ///     Create a new activity scope.
        /// </summary>
        /// <param name="activityId">
        ///     The current activity Id (if any).
        /// </param>
        /// <param name="previousActivityId">
        ///     The previous activity Id (if any).
        /// </param>
        internal ActivityScope(Guid? activityId = null, Guid? previousActivityId = null)
        {
            _activityId = activityId;
            _previousActivityId = previousActivityId;

            if (_activityId.HasValue)
            {
                ActivityCorrelationManager.SetCurrentActivityId(_activityId.Value);
            }
            else
            {
                ActivityCorrelationManager.ClearCurrentActivityId();
            }

            ActivityCorrelationManager.SynchronizeEventSourceActivityIds();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // If the correlation manager does not have the expected activity Id, it's safer to not clean up.
            if (ActivityCorrelationManager.CurrentActivityId == _activityId)
            {
                // Restore previous activity Id (if any).
                if (_previousActivityId.HasValue)
                {
                    ActivityCorrelationManager.SetCurrentActivityId(_previousActivityId.Value);
                }
                else
                {
                    ActivityCorrelationManager.ClearCurrentActivityId();
                }

                ActivityCorrelationManager.SynchronizeEventSourceActivityIds();
            }
        }
    }
}
