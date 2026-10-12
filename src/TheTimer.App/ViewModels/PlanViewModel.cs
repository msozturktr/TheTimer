using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheTimer.Core.Notifications;
using TheTimer.Core.Planning;
using TheTimer.Core.Sessions;

namespace TheTimer.App.ViewModels;

/// <summary>Plan tab: day navigation, occurrence list, planned-vs-actual summary, editor and block-start reminders.</summary>
public sealed partial class PlanViewModel : ViewModelBase
{
    private static readonly CultureInfo Tr = new("tr-TR");
    private const double EditorReserve = 366; // card width + gap, kept free on the ribbon while editing
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromSeconds(30);

    private readonly PlanService _service;
    private readonly ISessionRepository _sessions;
    private readonly TimeProvider _time;
    private readonly INotifier _notifier;
    private readonly Func<string, string?, bool> _startFromPlan;
    private readonly DispatcherTimer _reminderTimer;
    private readonly HashSet<(Guid BlockId, DateOnly Day)> _notified = [];
    private DateTimeOffset _lastCheck;
    private DateOnly _today;
    private PlanOccurrence? _editing;
    private PlanOccurrence? _pendingDelete;

    public PlanViewModel(
        PlanService service, ISessionRepository sessions, TimeProvider time, INotifier notifier,
        Func<string, string?, bool> startFromPlan)
    {
        _service = service;
        _sessions = sessions;
        _time = time;
        _notifier = notifier;
        _startFromPlan = startFromPlan;

        _lastCheck = _time.GetUtcNow();
        _today = Today();
        SelectedDay = _today;
        foreach (var day in new[]
                 {
                     WeekDays.Monday, WeekDays.Tuesday, WeekDays.Wednesday, WeekDays.Thursday,
                     WeekDays.Friday, WeekDays.Saturday, WeekDays.Sunday,
                 })
            DayToggles.Add(new DayToggleViewModel(day));

        Refresh();

        _reminderTimer = new DispatcherTimer { Interval = ReminderInterval };
        _reminderTimer.Tick += (_, _) => OnReminderTick();
        _reminderTimer.Start();
    }

    public ObservableCollection<PlanOccurrenceItemViewModel> Items { get; } = [];

    public ObservableCollection<TaskComparisonItemViewModel> TaskRows { get; } = [];

    public ObservableCollection<DayToggleViewModel> DayToggles { get; } = [];

    /// <summary>Recently used tags (the day's blocks and sessions) offered as quick picks in the editor.</summary>
    public ObservableCollection<TagChipViewModel> RecentTags { get; } = [];

    public IReadOnlyList<string> RepeatOptions { get; } = ["Tekrar yok", "Her gün", "Hafta içi", "Özel günler"];

    [ObservableProperty]
    public partial DateOnly SelectedDay { get; set; }

    [ObservableProperty]
    public partial string DayText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    // Ribbon geometry: visible hour range (whole hours) and the "now" marker.
    [ObservableProperty]
    public partial int RangeStartHour { get; set; } = 8;

    [ObservableProperty]
    public partial int RangeEndHour { get; set; } = 20;

    /// <summary>Minutes since midnight of "now" when the selected day is today and inside the range; otherwise -1.</summary>
    [ObservableProperty]
    public partial double NowMinutes { get; set; } = -1;

    [ObservableProperty]
    public partial string NowText { get; set; } = string.Empty;

    /// <summary>Minute the ribbon should scroll to when shown: "now", else the first block.</summary>
    [ObservableProperty]
    public partial double FocusMinutes { get; set; }

    // Planned vs actual summary (right column).
    [ObservableProperty]
    public partial string PlannedTotalText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string ActualTotalText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string BlockCountText { get; set; } = "0 blok";

    [ObservableProperty]
    public partial double Ratio { get; set; }

    [ObservableProperty]
    public partial string RatioText { get; set; } = "%0";

    // Editor state.
    [ObservableProperty]
    public partial bool IsEditorOpen { get; set; }

    [ObservableProperty]
    public partial string EditorTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaskName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Tag { get; set; }

    /// <summary>Typed "HH:mm" start; <see cref="StartTime"/> is its parsed value (null when unparseable).</summary>
    [ObservableProperty]
    public partial string StartText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EndText { get; set; } = string.Empty;

    public TimeSpan? StartTime
    {
        get => ParseTime(StartText);
        set => StartText = FormatTime(value);
    }

    public TimeSpan? EndTime
    {
        get => ParseTime(EndText);
        set => EndText = FormatTime(value);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(ShowRecurrenceControls), nameof(ShowCustomDays), nameof(IsRepeatNone), nameof(IsRepeatEveryDay),
        nameof(IsRepeatWeekdays), nameof(IsRepeatCustom), nameof(CanEditDays))]
    public partial int RepeatIndex { get; set; }

    [ObservableProperty]
    public partial DateTime? EndDate { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    /// <summary>Anchor of the floating editor card: the edited block's span and column (new block: no connector).</summary>
    [ObservableProperty]
    public partial double EditorAnchorStart { get; set; }

    [ObservableProperty]
    public partial double EditorAnchorEnd { get; set; }

    [ObservableProperty]
    public partial bool EditorHasConnector { get; set; }

    [ObservableProperty]
    public partial int EditorAnchorColumn { get; set; }

    [ObservableProperty]
    public partial int EditorAnchorColumns { get; set; } = 1;

    /// <summary>Width kept free on the ribbon's right while the editor card is open.</summary>
    public double ReserveRight => IsEditorOpen ? EditorReserve : 0;

    // Delete confirmation state.
    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; set; }

    [ObservableProperty]
    public partial string ConfirmText { get; set; } = string.Empty;

    public bool IsNew => _editing is null;

    public bool IsEditingOneOff => _editing is { IsRecurring: false };

    public bool IsEditingRecurring => _editing is { IsRecurring: true };

    public bool CanEditRecurrence => IsNew || IsEditingRecurring;

    public bool ShowRecurrenceControls => RepeatIndex > 0;

    public bool ShowCustomDays => RepeatIndex == 3;

    /// <summary>The day chips are shown for every repeat mode but only clickable for "Özel".</summary>
    public bool CanEditDays => CanEditRecurrence && RepeatIndex == 3;

    public bool IsRepeatNone { get => RepeatIndex == 0; set { if (value) RepeatIndex = 0; } }

    public bool IsRepeatEveryDay { get => RepeatIndex == 1; set { if (value) RepeatIndex = 1; } }

    public bool IsRepeatWeekdays { get => RepeatIndex == 2; set { if (value) RepeatIndex = 2; } }

    public bool IsRepeatCustom { get => RepeatIndex == 3; set { if (value) RepeatIndex = 3; } }

    public string RecurrenceHint => IsEditingRecurring
        ? "Tekrar ayarı tüm seriye uygulanır."
        : IsEditingOneOff ? "Tek seferlik blok tekrarlı yapılamaz." : string.Empty;

    partial void OnSelectedDayChanged(DateOnly value)
    {
        CloseEditor();
        CancelDelete();
        Refresh();
        GoTodayCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(DayNavLabel));
    }

    partial void OnIsEditorOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ReserveRight));
        UpdateEditingFlags();
    }

    partial void OnRepeatIndexChanged(int value)
    {
        // Presets show their weekdays in the chips; "Özel" starts from whatever is currently selected.
        var preset = value switch { 1 => WeekDays.EveryDay, 2 => WeekDays.Weekdays, _ => WeekDays.None };
        if (preset != WeekDays.None)
            foreach (var toggle in DayToggles)
                toggle.IsChecked = (preset & toggle.Day) != 0;
    }

    // ---- Navigation ----

    [RelayCommand]
    private void PreviousDay() => SelectedDay = SelectedDay.AddDays(-1);

    [RelayCommand(CanExecute = nameof(CanGoToday))]
    private void GoToday() => SelectedDay = Today();

    /// <summary>Relative name of the shown day; the button itself always jumps back to today.</summary>
    public string DayNavLabel => (SelectedDay.DayNumber - Today().DayNumber) switch
    {
        0 => "Bugün",
        1 => "Yarın",
        -1 => "Dün",
        _ => SelectedDay.ToString("d MMM ddd", new System.Globalization.CultureInfo("tr-TR")),
    };

    /// <summary>"Bugün" is disabled while today is already shown.</summary>
    private bool CanGoToday() => SelectedDay != Today();

    [RelayCommand]
    private void NextDay() => SelectedDay = SelectedDay.AddDays(1);

    private DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), TimeZoneInfo.Local).DateTime);

    /// <summary>Reloads occurrences, actuals and summary for the selected day.</summary>
    public void Refresh()
    {
        var day = SelectedDay;
        DayText = FormatDay(day);
        Items.Clear();
        TaskRows.Clear();
        RecentTags.Clear();
        try
        {
            var occurrences = _service.GetOccurrences(day, TimeZoneInfo.Local);
            var sessions = _sessions.GetByDay(day, TimeZoneInfo.Local);
            var canStart = day == Today();
            var built = occurrences
                .Select(o => new PlanOccurrenceItemViewModel(
                    o, PlanComparison.ActualForOccurrence(o, sessions), canStart, Start, OpenEdit, RequestDelete))
                .ToList();
            AssignColumns(built);
            foreach (var item in built)
                Items.Add(item);
            UpdateEditingFlags();

            var rows = PlanComparison.ByTask(occurrences, sessions);
            foreach (var row in rows)
                TaskRows.Add(new TaskComparisonItemViewModel(row, TagFor(row.TaskName, occurrences, sessions)));
            var planned = TimeSpan.FromTicks(rows.Sum(r => r.Planned.Ticks));
            var actual = TimeSpan.FromTicks(rows.Sum(r => r.Actual.Ticks));
            SummaryText = $"Planlanan: {PlanFormat.FormatHm(planned)} · Gerçekleşen: {PlanFormat.FormatHm(actual)}";
            PlannedTotalText = PlanFormat.FormatHm(planned);
            ActualTotalText = PlanFormat.FormatHm(actual);
            BlockCountText = $"{occurrences.Count} blok";
            Ratio = planned > TimeSpan.Zero ? actual / planned : 0;
            RatioText = $"%{(int)Math.Round(Ratio * 100)}";
            IsEmpty = occurrences.Count == 0;

            foreach (var tag in occurrences.Select(o => o.Block.Tag).Concat(sessions.Select(s => s.Tag))
                         .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase).Take(3))
                RecentTags.Add(new TagChipViewModel(tag));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to load plan: {ex}");
            SummaryText = string.Empty;
            IsEmpty = true;
            ErrorText = "Plan yüklenemedi.";
        }

        UpdateRange();
        UpdateNow();
    }

    /// <summary>"12 Ekim, Pazartesi" (the year is added only when it is not the current one).</summary>
    private string FormatDay(DateOnly day) =>
        day.ToString(day.Year == _today.Year ? "d MMMM, dddd" : "d MMMM yyyy, dddd", Tr);

    private static string? TagFor(string task, IEnumerable<PlanOccurrence> occurrences, IEnumerable<TimeSession> sessions) =>
        occurrences.Where(o => string.Equals(o.Block.TaskName, task, StringComparison.OrdinalIgnoreCase))
            .Select(o => o.Block.Tag)
            .Concat(sessions.Where(s => string.Equals(s.TaskName.Trim(), task, StringComparison.OrdinalIgnoreCase)).Select(s => s.Tag))
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

    /// <summary>Greedy side-by-side columns for overlapping blocks (per overlap cluster).</summary>
    private static void AssignColumns(List<PlanOccurrenceItemViewModel> items)
    {
        var ordered = items.OrderBy(i => i.StartMinutes).ThenBy(i => i.EndMinutes).ToList();
        var cluster = new List<PlanOccurrenceItemViewModel>();
        var columnEnds = new List<double>();
        var clusterEnd = double.MinValue;

        void Close()
        {
            foreach (var item in cluster)
                item.ColumnCount = Math.Max(columnEnds.Count, 1);
            cluster.Clear();
            columnEnds.Clear();
        }

        foreach (var item in ordered)
        {
            if (cluster.Count > 0 && item.StartMinutes >= clusterEnd)
                Close();
            var column = columnEnds.FindIndex(end => end <= item.StartMinutes);
            if (column < 0)
            {
                columnEnds.Add(item.EndMinutes);
                column = columnEnds.Count - 1;
            }
            else
            {
                columnEnds[column] = item.EndMinutes;
            }

            item.Column = column;
            cluster.Add(item);
            clusterEnd = cluster.Count == 1 ? item.EndMinutes : Math.Max(clusterEnd, item.EndMinutes);
        }

        Close();
    }

    /// <summary>Default 08:00-20:00, widened to whole hours around any block outside it.</summary>
    private void UpdateRange()
    {
        var start = 8;
        var end = 20;
        foreach (var item in Items)
        {
            start = Math.Min(start, (int)Math.Floor(item.StartMinutes / 60));
            end = Math.Max(end, (int)Math.Ceiling(item.EndMinutes / 60));
        }

        RangeStartHour = start;
        RangeEndHour = Math.Min(end, 24);
    }

    /// <summary>Moves the "now" marker (selected day is today) and picks the scroll focus.</summary>
    private void UpdateNow()
    {
        var local = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), TimeZoneInfo.Local);
        var minutes = local.TimeOfDay.TotalMinutes;
        var showNow = SelectedDay == Today() && minutes >= RangeStartHour * 60 && minutes <= RangeEndHour * 60;
        NowMinutes = showNow ? minutes : -1;
        NowText = showNow ? $"{local:HH:mm} · ŞİMDİ" : string.Empty;
        FocusMinutes = showNow
            ? minutes
            : Items.Count > 0 ? Items.Min(i => i.StartMinutes) : RangeStartHour * 60;
    }

    private void UpdateEditingFlags()
    {
        foreach (var item in Items)
        {
            item.IsEditing = _editing is { } e && e.Block.Id == item.Occurrence.Block.Id && e.Day == item.Occurrence.Day;
            item.IsSqueezed = IsEditorOpen;
        }
    }

    private static TimeSpan? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = text.Trim().Replace('.', ':');
        return TimeSpan.TryParseExact(t, ["h\\:mm", "hh\\:mm"], CultureInfo.InvariantCulture, out var value)
            && value >= TimeSpan.Zero && value < TimeSpan.FromHours(24)
            ? value
            : null;
    }

    private static string FormatTime(TimeSpan? value) =>
        value is { } v ? string.Create(CultureInfo.InvariantCulture, $"{v.Hours:00}:{v.Minutes:00}") : string.Empty;

    private void Start(PlanOccurrence occurrence) =>
        _startFromPlan(occurrence.Block.TaskName, occurrence.Block.Tag);

    // ---- Editor ----

    [RelayCommand]
    private void NewBlock()
    {
        CancelDelete();
        _editing = null;
        EditorTitle = "Yeni blok";
        TaskName = string.Empty;
        Tag = null;
        var startHour = SelectedDay == Today()
            ? Math.Min(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), TimeZoneInfo.Local).Hour + 1, 22)
            : 9;
        StartTime = TimeSpan.FromHours(startHour);
        EndTime = TimeSpan.FromHours(startHour + 1);
        SetRecurrenceFields(null);
        EditorHasConnector = false;
        EditorAnchorStart = RangeStartHour * 60;
        EditorAnchorEnd = EditorAnchorStart;
        OpenEditor();
    }

    private void OpenEdit(PlanOccurrence occurrence)
    {
        CancelDelete();
        _editing = occurrence;
        var block = occurrence.Block;
        EditorTitle = "Bloğu düzenle";
        TaskName = block.TaskName;
        Tag = block.Tag;
        StartTime = block.Start.ToTimeSpan();
        EndTime = block.End.ToTimeSpan();
        SetRecurrenceFields(block.Recurrence);
        var item = Items.FirstOrDefault(i => i.Occurrence == occurrence);
        EditorHasConnector = true;
        EditorAnchorStart = block.Start.ToTimeSpan().TotalMinutes;
        EditorAnchorEnd = block.End.ToTimeSpan().TotalMinutes;
        EditorAnchorColumn = item?.Column ?? 0;
        EditorAnchorColumns = item?.ColumnCount ?? 1;
        OpenEditor();
    }

    private void SetRecurrenceFields(PlanRecurrence? recurrence)
    {
        foreach (var toggle in DayToggles)
            toggle.IsChecked = recurrence is not null && (recurrence.Days & toggle.Day) != 0;
        EndDate = recurrence?.EndDate?.ToDateTime(TimeOnly.MinValue);
        RepeatIndex = recurrence?.Days switch
        {
            null => 0,
            WeekDays.EveryDay => 1,
            WeekDays.Weekdays => 2,
            _ => 3,
        };
    }

    private void OpenEditor()
    {
        ErrorText = string.Empty;
        IsEditorOpen = true;
        NotifyEditorMode();
        UpdateEditingFlags();
    }

    private void NotifyEditorMode()
    {
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(IsEditingOneOff));
        OnPropertyChanged(nameof(IsEditingRecurring));
        OnPropertyChanged(nameof(CanEditRecurrence));
        OnPropertyChanged(nameof(CanEditDays));
        OnPropertyChanged(nameof(RecurrenceHint));
    }

    [RelayCommand]
    private void CancelEdit() => CloseEditor();

    private void CloseEditor()
    {
        if (!IsEditorOpen && _editing is null)
            return;
        IsEditorOpen = false;
        _editing = null;
        ErrorText = string.Empty;
        NotifyEditorMode();
        UpdateEditingFlags();
    }

    /// <summary>"Ekle" (new block) or "Kaydet" (one-off).</summary>
    [RelayCommand]
    private void Submit()
    {
        if (_editing is null)
            Save(() =>
            {
                var (start, end) = Times();
                if (RepeatIndex == 0)
                    _service.AddOneOff(SelectedDay, start, end, TaskName, Tag);
                else
                    _service.AddRecurring(BuildRecurrence(SelectedDay), start, end, TaskName, Tag);
            });
        else
            SaveEdit(PlanEditScope.ThisDayOnly);
    }

    [RelayCommand]
    private void SaveThisDay() => SaveEdit(PlanEditScope.ThisDayOnly);

    [RelayCommand]
    private void SaveFollowing() => SaveEdit(PlanEditScope.ThisAndFollowing);

    private void SaveEdit(PlanEditScope scope)
    {
        var editing = _editing;
        if (editing is null)
            return;
        Save(() =>
        {
            var (start, end) = Times();
            var block = editing.Block;
            var occurrence = editing;
            if (block.Recurrence is { } oldRec)
            {
                var newRec = BuildRecurrence(oldRec.StartDate);
                if (newRec != oldRec)
                {
                    // Series-wide change first; the occurrence edit then runs against the updated series.
                    _service.UpdateRecurrence(block.Id, newRec);
                    occurrence = editing with { Block = block with { Recurrence = newRec } };
                }
            }

            var unchanged = start == block.Start && end == block.End
                && string.Equals(TaskName.Trim(), block.TaskName, StringComparison.Ordinal)
                && string.Equals(NormalizedTag(), block.Tag, StringComparison.Ordinal);
            if (!unchanged)
                _service.UpdateOccurrence(occurrence, start, end, TaskName, Tag, scope);
        });
    }

    private string? NormalizedTag() => string.IsNullOrWhiteSpace(Tag) ? null : Tag.Trim();

    private void Save(Action action)
    {
        try
        {
            Validate();
            action();
        }
        catch (ValidationException ex)
        {
            ErrorText = ex.Message;
            return;
        }
        catch (ArgumentException ex)
        {
            Trace.WriteLine($"Plan validation failed: {ex}");
            ErrorText = "Geçersiz değerler.";
            return;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to save plan: {ex}");
            ErrorText = "Kaydedilemedi.";
            return;
        }

        CloseEditor();
        Refresh();
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskName))
            throw new ValidationException("Görev adı gerekli.");
        if (StartTime is null || EndTime is null)
            throw new ValidationException("Başlangıç ve bitiş saati gerekli.");
        if (EndTime <= StartTime)
            throw new ValidationException("Bitiş, başlangıçtan sonra olmalı.");
        if (CanEditRecurrence && RepeatIndex == 3 && DayToggles.All(t => !t.IsChecked))
            throw new ValidationException("En az bir gün seçin.");
        var firstDay = _editing?.Block.Recurrence?.StartDate ?? SelectedDay;
        if (CanEditRecurrence && RepeatIndex > 0 && EndDate is { } end && DateOnly.FromDateTime(end) < firstDay)
            throw new ValidationException("Bitiş tarihi başlangıçtan önce olamaz.");
    }

    private (TimeOnly Start, TimeOnly End) Times() =>
        (TimeOnly.FromTimeSpan(StartTime!.Value), TimeOnly.FromTimeSpan(EndTime!.Value));

    private PlanRecurrence BuildRecurrence(DateOnly startDate)
    {
        var days = RepeatIndex switch
        {
            1 => WeekDays.EveryDay,
            2 => WeekDays.Weekdays,
            _ => DayToggles.Where(t => t.IsChecked).Aggregate(WeekDays.None, (acc, t) => acc | t.Day),
        };
        DateOnly? end = EndDate is { } e ? DateOnly.FromDateTime(e) : null;
        return new PlanRecurrence(startDate, end, days);
    }

    private sealed class ValidationException(string message) : Exception(message);

    // ---- Delete ----

    /// <summary>"Sil" in the editor card.</summary>
    [RelayCommand]
    private void DeleteEditing()
    {
        if (_editing is { } editing)
            RequestDelete(editing);
    }

    [RelayCommand]
    private void PickTag(string? tag) => Tag = tag;

    private void RequestDelete(PlanOccurrence occurrence)
    {
        if (!occurrence.IsRecurring)
        {
            Delete(occurrence, PlanEditScope.ThisDayOnly);
            return;
        }

        _pendingDelete = occurrence;
        ConfirmText = $"“{occurrence.Block.TaskName}” tekrarlı bir blok. Hangisi silinsin?";
        IsConfirmingDelete = true;
    }

    [RelayCommand]
    private void ConfirmDeleteThisDay() => ConfirmDelete(PlanEditScope.ThisDayOnly);

    [RelayCommand]
    private void ConfirmDeleteFollowing() => ConfirmDelete(PlanEditScope.ThisAndFollowing);

    [RelayCommand]
    private void CancelDelete()
    {
        _pendingDelete = null;
        IsConfirmingDelete = false;
    }

    private void ConfirmDelete(PlanEditScope scope)
    {
        if (_pendingDelete is { } occurrence)
            Delete(occurrence, scope);
        CancelDelete();
    }

    private void Delete(PlanOccurrence occurrence, PlanEditScope scope)
    {
        try
        {
            _service.DeleteOccurrence(occurrence, scope);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to delete plan block: {ex}");
            ErrorText = "Silinemedi.";
            return;
        }

        if (_editing?.Block.Id == occurrence.Block.Id)
            CloseEditor();
        Refresh();
    }

    // ---- Day rollover + reminders ----

    private void OnReminderTick()
    {
        try
        {
            var now = _time.GetUtcNow();
            var today = Today();
            UpdateNow();
            GoTodayCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(DayNavLabel));
            if (today != _today)
            {
                var follow = SelectedDay == _today;
                _today = today;
                _notified.Clear();
                if (follow)
                    SelectedDay = today;
                else
                    Refresh();
            }

            var due = PlanReminder.DueBetween(_service.GetOccurrences(today, TimeZoneInfo.Local), _lastCheck, now);
            _lastCheck = now;
            foreach (var o in due)
            {
                if (!_notified.Add((o.Block.Id, o.Day)))
                    continue;
                var start = TimeZoneInfo.ConvertTime(o.StartsAt, TimeZoneInfo.Local);
                var end = TimeZoneInfo.ConvertTime(o.EndsAt, TimeZoneInfo.Local);
                _ = NotifyAsync(
                    $"{start:HH:mm} — {o.Block.TaskName}",
                    $"Planlanan blok başlıyor ({start:HH:mm}–{end:HH:mm})");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Plan reminder check failed: {ex}");
        }
    }

    private async Task NotifyAsync(string title, string body)
    {
        try
        {
            await _notifier.NotifyAsync(title, body);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Notification failed: {ex}");
        }
    }
}
