using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheTimer.App.Services;
using TheTimer.Core.Settings;

namespace TheTimer.App.ViewModels;

/// <summary>Completion sound preferences (on/off and which sound), persisted through <see cref="JsonSettingsStore"/>.</summary>
public partial class MainWindowViewModel
{
    private JsonSettingsStore? _settingsStore;
    private bool _loadingSettings;

    public IReadOnlyList<SoundOption> SoundOptions => CompletionSound.Options;

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; } = true;

    [ObservableProperty]
    public partial SoundOption SelectedSound { get; set; } = CompletionSound.Options[0];

    /// <summary>Loads saved preferences and saves later changes to <paramref name="store"/>.</summary>
    public void AttachSettings(JsonSettingsStore store)
    {
        _settingsStore = store;
        var settings = store.Load();
        _loadingSettings = true;
        SoundEnabled = settings.SoundEnabled;
        SelectedSound = SoundOptions.FirstOrDefault(o => o.Id == settings.Sound) ?? SoundOptions[0];
        _loadingSettings = false;
    }

    partial void OnSoundEnabledChanged(bool value) => SaveSettings();

    partial void OnSelectedSoundChanged(SoundOption value) => SaveSettings();

    [RelayCommand]
    private void PreviewSound() => CompletionSound.Play(SelectedSound.Id);

    private void PlayCompletionSound()
    {
        if (SoundEnabled)
            CompletionSound.Play(SelectedSound.Id);
    }

    private void SaveSettings()
    {
        if (!_loadingSettings)
            _settingsStore?.Save(new AppSettings { SoundEnabled = SoundEnabled, Sound = SelectedSound.Id });
    }
}
