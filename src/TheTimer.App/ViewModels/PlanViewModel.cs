using System.Collections.ObjectModel;
using System.Diagnostics;
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

    public IReadOnlyList<string> RepeatOptions { get; } = ["Tekrar yok", "Her gün", "Hafta içi", "Özel günler"];

    [ObservableProperty]
    public partial DateOnly SelectedDay { get; set; }

    [ObservableProperty]
    public partial string DayText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    // Editor state.
    [ObservableProperty]
    public partial bool IsEditorOpen { get; set; }

    [ObservableProperty]
    public partial string EditorTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TaskName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Tag { get; set; }

    [ObservableProperty]
    public partial TimeSpan? StartTime { get; set; }

    [ObservableProperty]
    public partial TimeSpan? EndTime { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRecurrenceControls), nameof(ShowCustomDays))]
    public partial int RepeatIndex { get; set; }

    [ObservableProperty]
    public partial DateTime? EndDate { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

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

    public string RecurrenceHint => IsEditingRecurring
        ? "Tekrar ayarı tüm seriye uygulanır."
        : IsEditingOneOff ? "Tek seferlik blok tekrarlı yapılamaz." : string.Empty;

    partial void OnSelectedDayChanged(DateOnly value)
    {
        CloseEditor();
        CancelDelete();
        Refresh();
    }

    // ---- Navigation ----

    [RelayCommand]
    private void PreviousDay() => SelectedDay = SelectedDay.AddDays(-1);

    [RelayCommand]
    private void GoToday() => SelectedDay = Today();

    [RelayCommand]
    private void NextDay() => SelectedDay = SelectedDay.AddDays(1);

    private DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), TimeZoneInfo.Local).DateTime);

    /// <summary>Reloads occurrences, actuals and summary for the selected day.</summary>
    public void Refresh()
    {
        var day = SelectedDay;
        DayText = PlanFormat.LongDate(day);
        Items.Clear();
        TaskRows.Clear();
        try
        {
            var occurrences = _service.GetOccurrences(day, TimeZoneInfo.Local);
            var sessions = _sessions.GetByDay(day, TimeZoneInfo.Local);
            var canStart = day == Today();
            foreach (var o in occurrences)
                Items.Add(new PlanOccurrenceItemViewModel(
                    o, PlanComparison.ActualForOccurrence(o, sessions), canStart, Start, OpenEdit, RequestDelete));

            var rows = PlanComparison.ByTask(occurrences, sessions);
            foreach (var row in rows)
                TaskRows.Add(new TaskComparisonItemViewModel(row));
            var planned = TimeSpan.FromTicks(rows.Sum(r => r.Planned.Ticks));
            var actual = TimeSpan.FromTicks(rows.Sum(r => r.Actual.Ticks));
            SummaryText = $"Planlanan: {PlanFormat.FormatHm(planned)} · Gerçekleşen: {PlanFormat.FormatHm(actual)}";
            IsEmpty = occurrences.Count == 0;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to load plan: {ex}");
            SummaryText = string.Empty;
            IsEmpty = true;
            ErrorText = "Plan yüklenemedi.";
        }
    }

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
    }

    private void NotifyEditorMode()
    {
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(IsEditingOneOff));
        OnPropertyChanged(nameof(IsEditingRecurring));
        OnPropertyChanged(nameof(CanEditRecurrence));
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
