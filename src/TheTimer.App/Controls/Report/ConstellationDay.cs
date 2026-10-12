namespace TheTimer.App.Controls.Report;

/// <summary>One column of <see cref="ConstellationChart"/>.</summary>
public sealed record ConstellationDay(
    string Label,
    double ActualHours,
    double PlannedHours,
    bool IsToday,
    string ActualText,
    string PlannedText,
    string ToolTip);
