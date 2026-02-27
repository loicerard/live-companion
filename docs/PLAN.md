# Plan d'implémentation — Live Companion

> Dernière mise à jour : 2026-02-27
> Statut : en attente de validation

## Résumé de l'état actuel

### Issues terminées (mergées sur main)

| #   | Référence     | Description                          |
| --- | ------------- | ------------------------------------ |
| #1  | US-UI-01      | Structure WPF + MVVM                 |
| #2  | US-UI-02      | Navigation globale + état actif      |
| #11 | US-ARCH-01    | Interfaces cœur (Core)               |
| #12 | US-ARCH-02    | Implémentations Mock (EngineMock)    |
| #22 | US-LIVE-02    | États transport UI (mock)            |
| #23 | US-CONF-01    | Configuration Audio (mock)           |
| #24 | US-CONF-02    | Configuration MIDI (mock)            |

### PR en cours de test (branche `claude/discuss-next-issues-aEves`)

| #   | Référence     | Description                          |
| --- | ------------- | ------------------------------------ |
| #15 | US-SONG-01    | Créer un morceau (mock)              |
| #16 | US-SONG-02    | Éditer les sections (mock)           |

> Inclut aussi : `IProjectStore.GetAll()` / `Delete()`, `SectionViewModel` avec validation, `EditorView` complète.

---

## Stratégie globale

**Approche : Mock complet d'abord, puis implémentations réelles.**

La logique est simple : valider l'ensemble du flux UX sans dépendance matérielle (ASIO, MIDI), puis "brancher" les moteurs réels une fois l'UI et les interactions stabilisées.

L'ordre des phases suit les dépendances naturelles du projet :

```
[Song Mock] → [Live Rules + Store] → [Qualité] → [Audio Réel] → [MIDI Réel] → [Persistence] → [Live Sécurisé]
```

---

## Phase 1 — Éditeur complet : Samples, MIDI, Clic, Timeline

**Objectif :** Compléter l'écran Éditeur avec tous les éléments mock restants.
**Prérequis :** PR #15/#16 mergée.

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #18 | US-SONG-04    | Gérer les samples (mock)             | Haute    |
| #19 | US-SONG-05    | Gérer les événements MIDI (mock)     | Haute    |
| #20 | US-SONG-06    | Importer une piste de clic (mock)    | Moyenne  |
| #17 | US-SONG-03    | Timeline visuelle (mock)             | Haute    |

### Ordre d'implémentation recommandé

#### 1. #18 — Samples (mock)

**Quoi :** Ajouter/éditer/supprimer des `AudioClip` dans un morceau via l'éditeur.

**Fichiers à modifier :**
- `EditorViewModel.cs` — Ajouter une collection `ObservableCollection<AudioClipViewModel>`, commandes CRUD
- Nouveau : `AudioClipViewModel.cs` — Sub-VM avec validation (nom requis, volume 0-1, fades ≥ 0, position valide)
- `EditorView.xaml` — Nouveau panneau "Samples" sous les sections (liste + formulaire détail)
- `Song.cs` — Déjà la propriété `AudioClips`, rien à changer

**Approche :**
- Pattern identique aux sections : liste à gauche, détail à droite
- Position via 4 champs : Section (combo), Bar, Beat, Tick
- Bus : TextBox libre (mock, pas de validation bus existant)
- SyncMode : ComboBox (Free / BarAligned)
- Les fades sont en secondes (pas ms) pour simplicité

#### 2. #19 — Événements MIDI (mock)

**Quoi :** Programmer des événements MIDI à des positions précises + bouton Test.

**Fichiers à modifier :**
- `EditorViewModel.cs` — Ajouter collection + commandes CRUD pour `MidiEvent`
- Nouveau : `MidiEventViewModel.cs` — Sub-VM (type, channel 1-16, data1, data2, deviceOut, position)
- `EditorView.xaml` — Nouveau panneau "MIDI" (ou onglet séparé dans l'éditeur)
- `IMidiEngine.cs` — Déjà `SendEventAsync`, utilisable pour le bouton Test

**Approche :**
- Type : ComboBox (ProgramChange, ControlChange, NoteOn, NoteOff)
- DeviceOut : ComboBox alimenté par `IMidiEngine.GetAvailablePorts()`
- Bouton "Test" → appelle `_midiEngine.SendEventAsync(evt)` → log Debug
- Afficher Data2 uniquement si Type ≠ ProgramChange

#### 3. #20 — Piste de clic (mock)

**Quoi :** Associer un fichier clic à un morceau (chemin uniquement).

**Fichiers à modifier :**
- `EditorViewModel.cs` — Propriété `ClickTrackPath`, commande `BrowseClickTrack` + `ClearClickTrack`
- `EditorView.xaml` — Petit panneau "Piste de clic" (TextBlock chemin + boutons)
- `Song.cs` — Déjà `ClickTrackPath`, rien à changer

**Approche :**
- `OpenFileDialog` pour sélectionner le fichier (filtres : *.wav, *.mp3, *.flac, *.aiff)
- Affichage du nom du fichier (pas le chemin complet)
- Bus automatiquement assigné à "Click" (affiché en lecture seule)

#### 4. #17 — Timeline visuelle (mock)

**Quoi :** Représentation visuelle de la timeline du morceau courant.

**Fichiers à créer :**
- Nouveau : `TimelineControl.xaml` / `.cs` — UserControl custom dessiné
- `EditorView.xaml` ou `LiveView.xaml` — Intégrer le contrôle

**Approche :**
- Canvas WPF avec rectangles proportionnels par section (largeur = nb mesures × largeur fixe par mesure)
- Couleurs alternées par section + nom affiché
- Curseur vertical (ligne) positionné via binding sur `TimelinePosition`
- Info affichée : section courante (highlight), tempo, signature
- Zoom horizontal : slider qui modifie la largeur par mesure (min 20px, max 80px)
- En mode mock, le curseur avance piloté par le `TimelineSchedulerMock`

**Risques :**
- Performance du rendu Canvas si beaucoup de sections → mitigation : virtualisation pas nécessaire en V1 (max ~20 sections)

---

## Phase 2 — Live Rules + Store mémoire

**Objectif :** Connecter l'éditeur au Live et permettre la persistance en mémoire.
**Prérequis :** Phase 1 terminée.

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #21 | US-LIVE-01    | Règles bouton Next Section (mock)    | Haute    |
| #25 | US-PERS-01    | Projet en mémoire (mock store)       | Haute    |

### Ordre d'implémentation

#### 1. #25 — ProjectStore mock complet

**Quoi :** Compléter le store mémoire avec CRUD complet (Song, Playlist, Settings).

**Fichiers à modifier :**
- `IProjectStore.cs` — Ajouter : `Update(Song)`, `GetSettings()`, `SaveSettings()`, gestion Playlist
- `ProjectStoreMock.cs` — Implémenter les nouvelles méthodes

**Note :** La PR en cours a déjà ajouté `GetAll()` et `Delete()`. Il reste :
- `Update(Song)` pour persister les modifications
- Gestion d'une `Playlist` (liste ordonnée de `Song.Id`)
- Stockage de `Settings` (config audio/MIDI en mémoire)

**Approche :**
- Tout reste en `Dictionary` en mémoire
- Préparer l'interface pour le hook JSON futur (mêmes signatures)

#### 2. #21 — Règles Next Section (mock)

**Quoi :** Le bouton Next Section ne fonctionne que si aucun sample ne joue OU si Stop vient d'être pressé.

**Fichiers à modifier :**
- `LiveViewModel.cs` — Logique conditionnelle sur `NextSectionCommand`
- `TimelineSchedulerMock.cs` — Déjà `CanTransitionNow`, vérifier l'intégration avec `AudioEngineMock`
- `AudioEngineMock.cs` — Exposer un état `HasActiveVoices` (déjà prévu via le delegate `_hasActiveVoices`)
- `LiveView.xaml` — Indicateur visuel prêt/pas prêt (pastille verte/rouge à côté du bouton)

**Approche :**
- Le `TimelineSchedulerMock` reçoit déjà un `Func<bool> hasActiveVoices` → connecter via DI
- Flag `_justStopped` temporaire dans le transport (se réinitialise au prochain Play)
- UI : bouton grisé (`IsEnabled` bindé) + pastille visuelle

---

## Phase 3 — Qualité & Architecture

**Objectif :** Stabiliser le code, ajouter les tests, et permettre le switch Mock/Real.
**Prérequis :** Phase 2 terminée.

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #14 | US-ARCH-04    | Switch Mock ↔ Real                   | Haute    |
| #43 | US-QUAL-01    | Logging unifié                       | Moyenne  |
| #46 | US-QUAL-05    | Tests modèle (Song, Section, etc.)   | Moyenne  |
| #45 | US-QUAL-04    | Tests automatiques des mocks         | Moyenne  |

### Ordre d'implémentation

#### 1. #14 — Switch Mock ↔ Real

**Quoi :** Paramètre pour basculer entre Mock et Real au démarrage.

**Fichiers à modifier :**
- `appsettings.json` (nouveau) — `{ "EngineMode": "Mock" }`
- `App.xaml.cs` — Lire la config au démarrage
- `ServiceCollectionExtensions.cs` — Brancher Mock ou Real selon `EngineMode`
- `EngineReal/` — Créer des implémentations stub (throw `NotImplementedException`) pour compilation

**Approche :**
- `IConfiguration` de Microsoft.Extensions.Configuration
- Enum `EngineMode` déjà existant dans Core
- Argument CLI `--engine=real` override la config JSON

#### 2. #43 — Logging unifié

**Quoi :** Remplacer les `Debug.WriteLine` par un système structuré.

**Fichiers à modifier :**
- Tous les fichiers Mock (remplacer `Debug.WriteLine`)
- Nouveau : `ILogService.cs` dans Core
- Nouveau : `DebugLogService.cs` — Implémentation par défaut (Debug.WriteLine + buffer en mémoire)
- `MainWindow.xaml` — Panneau de debug optionnel (toggle F12)

**Approche :**
- Interface simple : `Log(LogLevel, string source, string message)`
- Sources : `Core`, `EngineMock`, `EngineReal`, `UI`
- Console debug interne : `ListBox` scrollable avec filtrage par source

#### 3. #46 — Tests modèle

**Quoi :** Tests unitaires sur Song, Section, AudioClip, TimelinePosition.

**Fichiers à créer :**
- Nouveau projet : `LiveCompanion.Tests`
- `SongTests.cs`, `SectionTests.cs`, `AudioClipTests.cs`, `TimelinePositionTests.cs`

**Approche :**
- xUnit + FluentAssertions
- Valider les valeurs par défaut, les limites (tempo 20-300, barCount ≥ 1), TimeSignature

#### 4. #45 — Tests des mocks

**Quoi :** Tests unitaires des implémentations mock.

**Fichiers à créer :**
- `AudioEngineMockTests.cs`, `TransportControllerMockTests.cs`, `TimelineSchedulerMockTests.cs`, `ProjectStoreMockTests.cs`

**Approche :**
- Tester les transitions d'état transport (Play/Pause/Stop)
- Tester l'avancement de la timeline (sections, beats)
- Tester le CRUD du ProjectStore
- Vérifier le thread-safety (tests parallèles)

---

## Phase 4 — Audio réel : Fondations

**Objectif :** Détection ASIO, sélection driver/buffer, routing des bus.
**Prérequis :** Phase 3 (switch Mock/Real fonctionnel).

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #26 | US-AUD-01     | Détection drivers ASIO               | Haute    |
| #27 | US-AUD-02     | Sélection driver & buffer size       | Haute    |
| #28 | US-AUD-03     | Buses logiques → sorties physiques   | Haute    |
| #29 | US-AUD-04     | Préchargement RAM des fichiers       | Haute    |

### Ordre d'implémentation

#### 1. #26 — Détection ASIO

**Fichiers :**
- `EngineReal/AudioEngineReal.cs` — Implémenter `GetAvailableDrivers()`
- NuGet : `NAudio` ou `ManagedBass` + `ManagedBass.Asio`

**Approche :**
- NAudio : `AsioOut.GetDriverNames()` — simple et éprouvé
- Alternative BASS : plus performant pour le multi-voix futur
- **Recommandation : NAudio pour la V1** (plus simple, communauté .NET plus large)
- Gestion erreur si aucun driver installé

#### 2. #27 — Driver & buffer

**Fichiers :**
- `AudioEngineReal.cs` — `InitializeAsync(config)`, `GetSupportedBufferSizes()`
- `ConfigViewModel.cs` — Connecter aux vraies valeurs

**Approche :**
- Ouvrir le driver ASIO, lister les buffer sizes supportés
- Redémarrage propre si changement de driver (Shutdown → Init)
- Détection instabilité : timer watchdog si le callback audio ne revient pas

#### 3. #28 — Buses → sorties physiques

**Fichiers :**
- Nouveau : `AudioBus.cs` dans Core (modèle)
- `AudioEngineReal.cs` — Mapping bus → channels de sortie ASIO
- `ConfigView.xaml` — UI de mapping (déjà partiellement en place)

**Approche :**
- Un bus = un nom + une paire de canaux de sortie (stéréo)
- Par défaut : "Main" → sorties 1-2, "Click" → sorties 3-4
- Validation : empêcher un bus sans sortie assignée

#### 4. #29 — Préchargement RAM

**Fichiers :**
- Nouveau : `AudioCache.cs` dans EngineReal — Cache des PCM décodés
- NuGet : NAudio pour décodage MP3/FLAC/WAV

**Approche :**
- Tout décoder en PCM float 48kHz mono/stéréo au chargement du morceau
- `Dictionary<string, float[]>` — clé = chemin fichier
- WAV/AIFF : chargement direct
- MP3/FLAC : décodage via `MediaFoundationReader` (NAudio)
- Gestion mémoire : libérer au changement de morceau

**Risques :**
- Fichiers volumineux (un morceau complet = ~50 MB en PCM) → surveiller la RAM
- MediaFoundation n'est pas dispo sur toutes les machines → prévoir un fallback ou un message d'erreur clair

---

## Phase 5 — Audio réel : Lecture & Mix

**Objectif :** Lire des samples, mixer par bus, jouer le clic.
**Prérequis :** Phase 4 terminée.

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #30 | US-AUD-05     | Lecture PCM multi-voix (16)          | Haute    |
| #31 | US-AUD-06     | Mix par bus avec volume et fades     | Haute    |
| #32 | US-AUD-07     | Lecture piste de clic (audio)        | Moyenne  |

### Ordre d'implémentation

#### 1. #30 — Multi-voix PCM

**Fichiers :**
- `AudioEngineReal.cs` — `PlayClipAsync()`, `StopAllAsync()`
- Nouveau : `VoicePool.cs` — Gestion des 16 voix (allocator simple)

**Approche :**
- Pool de 16 voix pré-allouées
- Fire & Forget : une voix lit son buffer PCM jusqu'à la fin, puis se libère
- Pas de time-stretch V1
- Mixage dans le callback ASIO : sommer toutes les voix actives

#### 2. #31 — Mix par bus

**Fichiers :**
- `AudioEngineReal.cs` — Intégrer le gain par sample et les fades
- `VoicePool.cs` — Chaque voix connaît son bus de destination

**Approche :**
- Gain dB → facteur linéaire (Math.Pow(10, dB/20))
- Fade-in/out : rampe linéaire appliquée sample par sample
- Chaque bus accumule les voix qui lui sont assignées
- Le callback ASIO route chaque bus vers ses canaux de sortie

#### 3. #32 — Clic audio

**Fichiers :**
- `AudioEngineReal.cs` — Voix spéciale pour le clic (toujours routée vers bus "Click")

**Approche :**
- Le clic est un seul fichier audio joué en boucle, synchronisé avec le transport
- Position alignée sur le beat (scheduler déclenche le playback)
- Routage fixe vers le bus "Click"

---

## Phase 6 — Audio réel : Transport & Scheduler

**Objectif :** Transport réel et scheduler sample-accurate.
**Prérequis :** Phase 5 terminée.

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #33 | US-AUD-08     | Transport réel (play/pause/stop)     | Haute    |
| #34 | US-AUD-09     | Scheduler sample-accurate            | Haute    |
| #35 | US-AUD-10     | Transition section immédiate (V1)    | Haute    |

### Ordre d'implémentation

#### 1. #33 — Transport réel

**Fichiers :**
- `EngineReal/TransportControllerReal.cs` — Coordonne AudioEngine + Scheduler
- Connecter le callback ASIO au transport

**Approche :**
- Play : démarrer le callback ASIO + scheduler
- Pause : arrêter immédiatement les samples (V1 : coupe franche)
- Stop : purger toutes les voix + reset position

#### 2. #34 — Scheduler sample-accurate

**Fichiers :**
- `EngineReal/TimelineSchedulerReal.cs`
- Nouveau : `TickClock.cs` — Horloge basée sur le compteur de samples ASIO

**Approche :**
- Conversion position (section/bar/beat/tick) → offset en samples (basée sur tempo + sample rate)
- L'horloge avance dans le callback ASIO (incrémente le compteur de samples)
- À chaque callback : vérifier si des événements doivent être déclenchés
- Précision : sample-level (pas de timer système)
- Ordonner les événements simultanés : MIDI avant audio (convention)

#### 3. #35 — Transition section immédiate

**Fichiers :**
- `TimelineSchedulerReal.cs` — `NextSectionAsync()` + `CanTransitionNow`

**Approche :**
- Règle V1 identique au mock : transition seulement si pas de sample actif OU stop récent
- En réel : vérifier `VoicePool.ActiveCount == 0`
- La transition remet le compteur de samples au début de la section suivante

---

## Phase 7 — MIDI réel

**Objectif :** Détection, envoi et scheduling MIDI.
**Prérequis :** Phase 4 minimum (driver ASIO initialisé pour l'horloge).

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #36 | US-MIDI-01    | Détection devices MIDI OUT           | Haute    |
| #37 | US-MIDI-02    | Envoi PC/CC/NoteOn/Off (réel)        | Haute    |
| #38 | US-MIDI-03    | Scheduler MIDI (±1ms, 6 devices)     | Haute    |

### Ordre d'implémentation

#### 1. #36 — Détection MIDI OUT

**Fichiers :**
- `EngineReal/MidiEngineReal.cs` — `GetAvailablePorts()`
- NuGet : `NAudio.Midi` ou `RtMidi.Net`

**Approche :**
- `MidiOut.NumberOfDevices` + `MidiOut.DeviceInfo(i)`
- Ouverture/fermeture propre des ports
- Gestion erreurs (device busy, déconnecté)

#### 2. #37 — Envoi MIDI

**Fichiers :**
- `MidiEngineReal.cs` — `SendEventAsync()`

**Approche :**
- Map `MidiEventType` → messages MIDI bruts (status byte + data bytes)
- Support multi-device : dictionnaire `deviceName → MidiOut`
- Ouvrir les ports à l'init, fermer au shutdown

#### 3. #38 — Scheduler MIDI

**Fichiers :**
- `TimelineSchedulerReal.cs` — Intégrer les événements MIDI dans le scheduling

**Approche :**
- Même horloge sample-accurate que l'audio
- Précision ±1ms = ±48 samples à 48kHz (largement dans la précision sample-level)
- File d'événements triée par position absolue (en samples)
- Gestion multi-devices : les événements sont dispatchés au bon `MidiOut`

---

## Phase 8 — Persistence réelle (JSON)

**Objectif :** Sauvegarder et charger les projets sur le disque.
**Prérequis :** Phase 2 (#25 — store mémoire).

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #39 | US-PERS-02    | Sauvegarde JSON complète             | Haute    |
| #40 | US-PERS-03    | Chargement JSON avec validation      | Haute    |
| #41 | US-PERS-04    | Gestion des playlists                | Moyenne  |
| #42 | US-PERS-05    | Sauvegarde auto (autosave)           | Moyenne  |

### Ordre d'implémentation

#### 1. #39 — Sauvegarde JSON

**Fichiers :**
- Nouveau : `EngineReal/ProjectStoreReal.cs` — Implémente `IProjectStore` avec fichiers JSON
- Documenter le schéma JSON (dans le code ou un fichier séparé)

**Approche :**
- `System.Text.Json` avec options indentées
- Structure : `{ song: { ... }, audioClips: [...], midiEvents: [...], clickTrackPath: "..." }`
- Chemins audio : relatifs au dossier projet (portabilité)
- Validation avant sauvegarde : vérifier cohérence modèle

#### 2. #40 — Chargement JSON

**Fichiers :**
- `ProjectStoreReal.cs` — `LoadAsync()`

**Approche :**
- Désérialisation + validation des contraintes métier (tempo, barCount, etc.)
- Vérification existence des fichiers audio référencés
- Messages d'erreur clairs : fichier manquant, JSON malformé, version incompatible

#### 3. #41 — Playlists

**Fichiers :**
- Nouveau : `Playlist.cs` dans Core
- `IProjectStore.cs` — Méthodes playlist
- `ProjectStoreReal.cs` — Sérialisation playlist

**Approche :**
- Playlist = liste ordonnée de `Song.Id` + titre
- Vérification cohérence : un morceau référencé doit exister
- Fichier séparé : `playlist.json`

#### 4. #42 — Autosave

**Fichiers :**
- Nouveau : `AutoSaveService.cs` dans Core ou UI
- `ConfigViewModel.cs` — Intervalle configurable

**Approche :**
- `DispatcherTimer` ou `System.Threading.Timer`
- Intervalle par défaut : 5 minutes
- Sauvegarde silencieuse (pas de popup) + log
- Pas de sauvegarde si aucune modification détectée (flag dirty)

---

## Phase 9 — Mode Live sécurisé

**Objectif :** Empêcher les actions destructives pendant un concert.
**Prérequis :** Toutes les phases précédentes.

### Issues

| #   | Référence     | Description                          | Priorité |
| --- | ------------- | ------------------------------------ | -------- |
| #44 | US-QUAL-03    | Mode Live sécurisé                   | Moyenne  |

### Implémentation

**Fichiers :**
- Nouveau : `LiveModeGuard.cs` dans Core — Service qui gère l'état "Live mode ON/OFF"
- `EditorViewModel.cs` — Vérifier `LiveModeGuard.IsLive` avant suppression/modification
- `LiveView.xaml` — Toggle "Mode Live" (cadenas visuel)
- Tous les ViewModels concernés — Griser les actions interdites

**Approche :**
- En mode Live : impossible de supprimer section, modifier samples, changer config audio
- Message clair si action bloquée : "Action non disponible en mode Live"
- Toggle via un bouton visible dans la LiveView (confirmation avant désactivation)

---

## Vue d'ensemble des phases

```
Phase 1 ─ Éditeur complet (mock)      │ #18, #19, #20, #17
Phase 2 ─ Live Rules + Store           │ #25, #21
Phase 3 ─ Qualité & Architecture       │ #14, #43, #46, #45
Phase 4 ─ Audio réel : Fondations      │ #26, #27, #28, #29
Phase 5 ─ Audio réel : Lecture & Mix   │ #30, #31, #32
Phase 6 ─ Audio réel : Transport       │ #33, #34, #35
Phase 7 ─ MIDI réel                    │ #36, #37, #38
Phase 8 ─ Persistence réelle           │ #39, #40, #41, #42
Phase 9 ─ Mode Live sécurisé           │ #44
```

**Total : 27 issues restantes réparties en 9 phases.**

---

## Dépendances clés entre phases

- **Phase 1 → Phase 2** : Les samples/MIDI doivent exister pour que les règles Live aient du sens
- **Phase 2 → Phase 3** : Le store mémoire doit être complet avant de tester
- **Phase 3 → Phase 4** : Le switch Mock/Real doit fonctionner pour commencer le réel
- **Phase 4 → Phase 5 → Phase 6** : Chaîne audio séquentielle (driver → lecture → transport)
- **Phase 4 → Phase 7** : Le MIDI a besoin de l'horloge audio pour le scheduling
- **Phase 2 → Phase 8** : La persistence réelle remplace le store mémoire
- **Toutes → Phase 9** : Le mode sécurisé protège des actions de toutes les phases

## Risques techniques identifiés

| Risque | Impact | Mitigation |
| ------ | ------ | ---------- |
| Performance ASIO callback | Dropouts audio | Profiler dès Phase 4, buffer size adaptatif |
| Compatibilité drivers ASIO | Certains drivers ne fonctionnent pas | Tester avec ASIO4ALL + interface physique |
| Décodage MP3/FLAC | MediaFoundation non dispo partout | Fallback message d'erreur clair, recommander WAV |
| Précision MIDI multi-device | Jitter > 1ms | Scheduler basé sur horloge ASIO (pas timer système) |
| Mémoire RAM avec gros projets | OOM sur fichiers longs | Monitoring mémoire + avertissement UI |
