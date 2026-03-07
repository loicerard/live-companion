using CommunityToolkit.Mvvm.ComponentModel;
using LiveCompanion.Core.Interfaces;

namespace LiveCompanion.UI.ViewModels;

/// <summary>
/// ViewModel représentant un canal du mixer (un par bus audio).
/// Expose le volume (two-way) et les niveaux VU (read-only, mis à jour par timer).
/// </summary>
public partial class BusMixerChannelViewModel : ObservableObject
{
    private readonly IAudioMixerProvider _mixer;

    public string BusName { get; }

    [ObservableProperty] private float _volume = 1.0f;
    [ObservableProperty] private float _levelLeft;
    [ObservableProperty] private float _levelRight;

    public BusMixerChannelViewModel(string busName, IAudioMixerProvider mixer)
    {
        BusName = busName;
        _mixer = mixer;
        _volume = mixer.GetBusVolume(busName);
    }

    partial void OnVolumeChanged(float value)
    {
        _mixer.SetBusVolume(BusName, value);
    }

    public void UpdateLevels(float left, float right)
    {
        LevelLeft = left;
        LevelRight = right;
    }
}
