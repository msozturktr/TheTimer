namespace TheTimer.App.Controls.Plan;

/// <summary>Data a <see cref="PlanTimeline"/> needs from a block's data context to position it.</summary>
public interface ITimelineItem
{
    double StartMinutes { get; }

    double EndMinutes { get; }

    /// <summary>Zero-based side-by-side column for overlapping blocks.</summary>
    int Column { get; }

    int ColumnCount { get; }
}
