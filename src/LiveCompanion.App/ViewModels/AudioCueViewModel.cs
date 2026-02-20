using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCompanion.App.Services;
using LiveCompanion.Core.Models;
using Microsoft.Win32;
using NAudio.Wave;

namespace LiveCompanion.App.ViewModels;

/// <summary>
/// Observable wrapper around <see cref="AudioCueEvent"/> that provides
/// Browse and Preview commands (Bug 2).
/// </summary>
public partial class AudioCueViewModel : ObservableObject, IDisposable
{
    private readonly AudioCueEvent _model;
    private WaveOutEvent? _previewOut;
    private AudioFileReader? _previewReader;

    public AudioCueViewModel(AudioCueEvent model)
    {
        _model = model;
        _filePath = model.SampleFileName;
        _gainDb   = model.GainDb;
    }

    // ── Bindable properties ──────────────────────────────────────────────

    /// <summary>
    /// Relative file name stored in the AudioCueEvent.
    /// Displayed in the UI; updated by the Browse command.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewCommand))]
    private string _filePath;

    [ObservableProperty]
    private double _gainDb;

    public long Tick => _model.Tick;

    // ── Commands ─────────────────────────────────────────────────────────

    /// <summary>
    /// Bug 2 — Browse:
    /// Opens an OpenFileDialog filtered to supported audio formats.
    /// The chosen file is copied into %AppData%\LiveCompanion\samples\
    /// and only the file name (relative path) is stored in the model.
    /// </summary>
    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title  = "Sélectionner un fichier audio",
            Filter = "Fichiers audio|*.mp3;*.wav;*.ogg;*.flac|Tous les fichiers|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var samplesDir = AppPathService.SamplesDirectory;
            Directory.CreateDirectory(samplesDir);

            var fileName = Path.GetFileName(dialog.FileName);
            var destPath = Path.Combine(samplesDir, fileName);

            // Copy to the samples directory (overwrite if already there)
            File.Copy(dialog.FileName, destPath, overwrite: true);

            // Store only the relative file name so the setlist is portable
            _model.SampleFileName = fileName;
            FilePath = fileName;

            Debug.WriteLine($"[Config] Sample copied: {dialog.FileName} → {destPath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible de copier le fichier :\n{ex.Message}",
                "Erreur",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Bug 2 — Preview:
    /// Plays the sample using the standard Windows audio output (WaveOutEvent),
    /// bypassing ASIO so the preview works even without an ASIO interface connected.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void Preview()
    {
        StopPreview();

        var fullPath = AppPathService.ResolveSamplePath(FilePath);
        if (!File.Exists(fullPath))
        {
            MessageBox.Show(
                $"Fichier introuvable :\n{fullPath}",
                "Aperçu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            _previewReader = new AudioFileReader(fullPath);
            _previewOut    = new WaveOutEvent();
            _previewOut.Init(_previewReader);
            _previewOut.PlaybackStopped += (_, _) => StopPreview();
            _previewOut.Play();

            Debug.WriteLine($"[Config] Preview started: {fullPath}");
        }
        catch (Exception ex)
        {
            StopPreview();
            MessageBox.Show(
                $"Erreur lors de la lecture :\n{ex.Message}",
                "Aperçu",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool CanPreview() => !string.IsNullOrWhiteSpace(FilePath);

    // ── Sync model write-back ────────────────────────────────────────────

    partial void OnGainDbChanged(double value) => _model.GainDb = value;

    // ── Cleanup ──────────────────────────────────────────────────────────

    private void StopPreview()
    {
        _previewOut?.Stop();
        _previewOut?.Dispose();
        _previewReader?.Dispose();
        _previewOut    = null;
        _previewReader = null;
    }

    public void Dispose() => StopPreview();
}
