using LiveCompanion.Core.Interfaces;
using LiveCompanion.EngineMock;
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

        // TimelineSchedulerMock nécessite un délégué Func<bool> hasActiveVoices.
        // On le câble à AudioEngineMock.ActiveVoices > 0.
        services.AddSingleton<ITimelineScheduler>(sp =>
        {
            var audioMock = sp.GetRequiredService<AudioEngineMock>();
            return new TimelineSchedulerMock(() => audioMock.ActiveVoices > 0);
        });

        return services;
    }

    private static IServiceCollection AddRealEngines(this IServiceCollection services)
    {
        // TODO: Enregistrer les implémentations réelles quand LiveCompanion.EngineReal
        //       fournira des classes concrètes. Pour l'instant, on lève une exception
        //       pour rendre toute mauvaise configuration évidente.
        throw new NotImplementedException(
            "Les implémentations moteur réelles ne sont pas encore disponibles. " +
            "Utilisez EngineMode.Mock.");
    }
}
