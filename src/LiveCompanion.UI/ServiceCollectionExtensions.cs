using LiveCompanion.Core.Interfaces;
using LiveCompanion.Core.Services;
using LiveCompanion.EngineMock;
using LiveCompanion.EngineReal;
using LiveCompanion.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LiveCompanion.UI;

/// <summary>
/// Méthodes d'extension pour configurer le conteneur DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre tous les services moteur selon le <paramref name="mode"/> sélectionné.
    /// </summary>
    public static IServiceCollection AddEngines(
        this IServiceCollection services,
        EngineMode mode)
    {
        // Logging unifié — singleton partagé par tous les moteurs et ViewModels
        services.AddSingleton<ILogService, DebugLogService>();

        return mode switch
        {
            EngineMode.Mock => services.AddMockEngines(),
            EngineMode.Real => services.AddRealEngines(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>
    /// Enregistre tous les ViewModels dans le conteneur DI.
    /// MainViewModel est singleton ; les ViewModels de fonctionnalité sont transient.
    /// Enregistre également les factories <c>Func&lt;T&gt;</c> pour la navigation.
    /// </summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // MainViewModel : singleton (un seul pour toute la durée de vie de l'application)
        services.AddSingleton<MainViewModel>();

        // ViewModels de fonctionnalité : transient (nouvelle instance par navigation)
        services.AddTransient<LiveViewModel>();
        services.AddTransient<EditorViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<ConfigViewModel>();

        // Factories pour que MainViewModel puisse créer les ViewModels à la demande.
        // Microsoft.Extensions.DI ne résout pas automatiquement Func<T>,
        // on les enregistre donc explicitement.
        services.AddSingleton<Func<LiveViewModel>>(sp
            => () => sp.GetRequiredService<LiveViewModel>());
        services.AddSingleton<Func<EditorViewModel>>(sp
            => () => sp.GetRequiredService<EditorViewModel>());
        services.AddSingleton<Func<LibraryViewModel>>(sp
            => () => sp.GetRequiredService<LibraryViewModel>());
        services.AddSingleton<Func<ConfigViewModel>>(sp
            => () => sp.GetRequiredService<ConfigViewModel>());

        return services;
    }

    // ------------------------------------------------------------------ //
    // Méthodes privées
    // ------------------------------------------------------------------ //

    private static IServiceCollection AddMockEngines(this IServiceCollection services)
    {
        // AudioEngineMock enregistré en tant que type concret ET en tant que IAudioEngine.
        // L'enregistrement concret est nécessaire pour que TimelineSchedulerMock
        // puisse accéder à la propriété ActiveVoices (absente de IAudioEngine).
        services.AddSingleton<AudioEngineMock>();
        services.AddSingleton<IAudioEngine>(sp
            => sp.GetRequiredService<AudioEngineMock>());

        services.AddSingleton<IMidiEngine, MidiEngineMock>();
        services.AddSingleton<ITransportController, TransportControllerMock>();
        services.AddSingleton<IProjectStore, ProjectStoreMock>();

        // TimelineSchedulerMock nécessite :
        // - ILogService pour le logging unifié
        // - un délégué Func<bool> hasActiveVoices (câblé à AudioEngineMock.ActiveVoices > 0)
        // - IAudioEngine pour déclencher les AudioClips aux bonnes positions
        // - IMidiEngine pour envoyer les MidiEvents aux bonnes positions
        services.AddSingleton<ITimelineScheduler>(sp =>
        {
            var log = sp.GetRequiredService<ILogService>();
            var audioMock = sp.GetRequiredService<AudioEngineMock>();
            var audioEngine = sp.GetRequiredService<IAudioEngine>();
            var midiEngine = sp.GetRequiredService<IMidiEngine>();
            return new TimelineSchedulerMock(
                log,
                () => audioMock.ActiveVoices > 0,
                audioEngine,
                midiEngine);
        });

        return services;
    }

    private static IServiceCollection AddRealEngines(this IServiceCollection services)
    {
        // ASIO abstraction + audio cache — required by AudioEngineReal
        services.AddSingleton<IAsioInterop, AsioInterop>();
        services.AddSingleton<AudioCache>();

        // AudioEngineReal enregistré en tant que type concret ET en tant que IAudioEngine.
        // L'enregistrement concret est nécessaire pour que le TimelineSchedulerReal
        // puisse accéder à VoicePool.ActiveCount (absent de IAudioEngine).
        services.AddSingleton<AudioEngineReal>();
        services.AddSingleton<IAudioEngine>(sp
            => sp.GetRequiredService<AudioEngineReal>());

        services.AddSingleton<IMidiEngine, MidiEngineReal>();
        services.AddSingleton<ITransportController, TransportControllerReal>();
        services.AddSingleton<IProjectStore, ProjectStoreReal>();

        // TimelineSchedulerReal requires the same wiring as Mock:
        // - ILogService for unified logging
        // - hasActiveVoices delegate wired to AudioEngineReal.VoicePool.ActiveCount > 0
        // - IAudioEngine for triggering AudioClips at timeline positions
        // - IMidiEngine for sending MidiEvents at timeline positions
        services.AddSingleton<ITimelineScheduler>(sp =>
        {
            var log = sp.GetRequiredService<ILogService>();
            var audioReal = sp.GetRequiredService<AudioEngineReal>();
            var audioEngine = sp.GetRequiredService<IAudioEngine>();
            var midiEngine = sp.GetRequiredService<IMidiEngine>();
            return new TimelineSchedulerReal(
                log,
                () => audioReal.ActiveVoices > 0,
                audioEngine,
                midiEngine);
        });

        return services;
    }
}
