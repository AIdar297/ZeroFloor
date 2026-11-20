# PROJECT_STRUCTURE.md — «Нулевой этаж»

> Практический справочник: что лежит где и куда класть новый файл. Концептуальное обоснование — в [TECHNICAL_ARCHITECTURE.md](TECHNICAL_ARCHITECTURE.md). Проект — SDK-style `.csproj` (`ZeroFloor.csproj`, `net10.0-windows`, `UseWPF=true`), поэтому любой `.cs`/`.xaml`-файл, добавленный в папку проекта, автоматически попадает в сборку — explicit `<ItemGroup>` править не нужно.

## 1. Дерево каталогов (создано на этом этапе)

```
ZeroFloor/                         ← корень проекта (.csproj здесь)
├── CLAUDE.md
├── TECHNICAL_ARCHITECTURE.md
├── PROJECT_STRUCTURE.md
├── PROJECT_ANALYSIS.md
├── App.xaml / App.xaml.cs         ← композиционный корень (регистрация сервисов)
├── MainWindow.xaml / .xaml.cs     ← окно-«хост», переключает текущий View по NavigationService
├── docs/                          ← исходные документы сценария (Story Bible, Flowchart)
│
├── Views/                         ← XAML-экраны + их code-behind
│   └── Controls/                  ← переиспользуемые UserControl (DialogueBox, ChoiceList, CharacterLayer)
│
├── ViewModels/                    ← по одной ViewModel на экран из Views/
│
├── Models/                        ← чистые данные: GameState, SceneDefinition, DialogueNode,
│                                      ChoiceDefinition, ConditionNode, Effect, CharacterDefinition,
│                                      EndingDefinition, SaveSlotData, AppSettings, FlagValue
│
├── Engine/                        ← игровые правила без WPF (реализовано): SceneManager,
│                                      DialogueManager, ConditionEvaluator, EffectApplier,
│                                      ChoiceProcessor, EndingManager, GameSessionController,
│                                      ScenarioValidator, ScenarioValidationException
│                                      (мутация K/L/R/H и флагов — методы самого GameState
│                                      в Models/, отдельный GameStateManager не понадобился)
│
├── Services/                      ← инфраструктура за интерфейсами
│   ├── Json/                       ← конвертеры сценарного JSON (FlagValueJsonConverter,
│   │                                   EffectJsonConverter, ComparisonOperatorJsonConverter,
│   │                                   ScenarioJsonOptions) + ScenarioRepository (реализовано)
│   └── Presentation/               ← WPF-специфичные сервисы визуальных эффектов (реализовано:
│                                      CharacterAnimationService, SceneTransitionService,
│                                      UiRevealService, AnimationPrimitives, StageLayout; план:
│                                      ITextRevealController) — единственное место в Services,
│                                      которому разрешено ссылаться на System.Windows.Media.
│                                      Получают WPF-элементы только от Views (не от ViewModel —
│                                      см. NovelView.xaml.cs), доступны через App.Current.
│
│       └── World3D/                ← интерактивные 3D-эпизоды (Главы 2А и 6) на WPF Viewport3D:
│                                      MeshBuilder (low-poly геометрия), Materials3D (процедурные
│                                      текстуры), CollisionWorld (AABB, рейкасты обзора/камеры),
│                                      HumanoidRig (модель+процедурная анимация Макса/NPC),
│                                      DroneModel, ParticleSystem3D, CameraRig, PlayerController,
│                                      EpisodeInput, EpisodeAudio (синтезированные звуки),
│                                      PortraitSkin (модель Макса по портрету),
│                                      EpisodeRuntime (база) → MarketStealthEpisode,
│                                      ShaftsChaseEpisode, ArchiveStealthEpisode. Хост — Views/Controls/
│                                      Episode3DOverlayControl, мост к сценарию —
│                                      ViewModels/Episode3DViewModel.
│
├── Common/                        ← ViewModelBase, RelayCommand, лёгкий мессенджер событий
│
├── Styles/                        ← ResourceDictionary: цвета, шрифты, стили контролов (Theme.xaml)
│
├── Data/                          ← игровой контент как данные (НЕ код)
│   ├── Scenario/                  ← JSON по главам: CH01_LOW_FLOW.json … CH12_FINALE.json,
│   │                                  CH11A_*.json / CH11B_*.json / CH11C_*.json / CH11D_*.json
│   │                                  (пока пусто — реальный сюжет ещё не перенесён)
│   ├── Demo/                      ← animation_showcase.json — демонстрационный сценарий для
│   │                                  проверки анимаций (NovelViewModel.DefaultStartSceneId);
│   │                                  явно НЕ сюжет, копируется в сборку (ZeroFloor.csproj)
│   └── Characters/                ← JSON-манифест CharacterDefinition (id → имя, путь к изображению)
│
└── Assets/                        ← бинарные ресурсы (изображения/звук), ждут реальных файлов от автора
    ├── Backgrounds/                ← фоны сцен (по локациям — раздел 12 Story Bible)
    ├── Characters/                 ← по ОДНОМУ изображению на персонажа (см. CLAUDE.md)
    ├── CG/                         ← отдельные иллюстрации для Галереи/эпилоговых карточек
    ├── UI/                         ← элементы интерфейса (курсоры, иконки, рамки)
    ├── Audio/                      ← реализовано: AudioResourceResolver ищет
    │   │                              <id>.wav|.mp3|.wma (id может содержать "/" для подпапок)
    │   ├── Music/                  ← музыкальные темы по сценам/главам (SceneDefinition.Music)
    │   ├── Ambience/               ← фоновые звуковые слои локаций (SceneDefinition.Ambience)
    │   ├── SFX/                    ← разовые звуковые эффекты (DialogueNode.SoundEffects)
    │   └── Voice/                  ← озвучка реплик, по персонажам: Voice/<characterId>/<id>.*
    │                                  (DialogueNode.VoiceClip); сейчас пусто — реальных записей
    │                                  нет, только test_tone_* заглушки в Music/Ambience/SFX
    │                                  (НЕ в Voice — см. CLAUDE.md и правило ниже)
    └── Fonts/                      ← шрифты интерфейса/текста реплик

ZeroFloor.Tests/                   ← отдельный проект (xUnit), рядом с ZeroFloor/, в том же .slnx
├── ZeroFloor.Tests.csproj          ← ProjectReference на ZeroFloor.csproj
├── *Tests.cs                       ← по одному файлу тестов на класс Engine/Services (плоско, без подпапок)
├── Support/                        ← ScenarioTestFactory — сборка тестовых сцен/узлов/выборов без JSON
└── Fixtures/                       ← JSON-сценарии ТОЛЬКО для тестов (CopyToOutputDirectory), с пометкой
                                        [ТЕСТ]/[ТЕСТОВЫЙ СЦЕНАРИЙ] в текстах — никогда не реальный сюжет
```

`Tests/` добавлен на этом этапе (Этап 2 плана, [TECHNICAL_ARCHITECTURE.md, раздел 13](TECHNICAL_ARCHITECTURE.md)) — теперь, когда `Engine` есть что тестировать: 101 тест в `ZeroFloor.Tests`, `dotnet test` зелёный. Тестовые сценарии в `Fixtures/` — синтетические, явно помеченные как тестовые данные; реальный сюжет по-прежнему живёт только в `Data/Scenario/*.json` (пока пусто) и не смешивается с тестовыми фикстурами.

## 2. Куда класть новый файл — быстрый справочник

| Я хочу добавить... | Файл идёт в | Что НЕ трогать |
|---|---|---|
| Новую главу/сцену сюжета | `Data/Scenario/CHxx_*.json` | `Engine/*` — движок уже умеет читать любую корректную сцену |
| Нового персонажа | 1 запись в `Data/Characters/characters.json` + 1 файл в `Assets/Characters/` | `Views/Controls/CharacterLayerControl` — он универсален по дизайну |
| Новый фон/CG | Файл в `Assets/Backgrounds/` или `Assets/CG/` + ссылка по тому же `id` в JSON сцены | `AssetService` — резолвит по ID, новый код не нужен |
| Новый трек/SFX | Файл в `Assets/Audio/Music|Ambience|SFX/` + ссылка по `id` в JSON сцены | `AudioService` |
| Новый экран (например, «Титры») | `Views/CreditsView.xaml` + `ViewModels/CreditsViewModel.cs` + точка в `INavigationService` | Остальные экраны — не меняются |
| Новую реализацию сервиса (напр. облачные сохранения) | Новый класс в `Services/`, реализующий существующий интерфейс | Код, который вызывает интерфейс (`GameSessionController`, ViewModels) |
| Новый тип визуального эффекта персонажа | Метод в `Services/Presentation/ICharacterAnimationService` + реализация через `Storyboard` | `Engine/*` — анимация не является игровым правилом |

## 3. Что НЕ размещается в коде (`.cs`)

- Тексты реплик, названия глав/сцен, формулировки выборов — только `Data/Scenario/*.json`.
- Числовые пороги концовок (K≥40, H≥70, father_file_found, sonya_exposed) и стартовые значения K/L/R/H=50/50/50/60 — зафиксированы как **контент** в [PROJECT_ANALYSIS.md, разделы 7–8](PROJECT_ANALYSIS.md) и выражаются в JSON-условиях (`ConditionNode`) конкретных глав, а не как константы, захардкоженные в `Engine`.
- Пути к файлам ассетов — только через `id` + манифест/каталог в `IAssetService`/`IAudioService`, никогда как строковый литерал пути внутри `ViewModel` или `Engine`.

## 4. Именование и соглашения

- **Namespace** по умолчанию — `ZeroFloor.<Папка>` (напр. `ZeroFloor.Engine`, `ZeroFloor.ViewModels`), строго по физической папке — поиск класса по namespace всегда приводит в правильную директорию.
- **Scene ID** — `CHnn_SHORT_NAME` для глав 1–10 и 12 (напр. `CH01_LOW_FLOW`), `CHnnX_SHORT_NAME` для подветок Главы 11 (`CH11A_CROWN_RECEPTION`, `CH11D_ANI_BIOMETRY`, `CH11D_THREE_TERMINALS`) — ровно те идентификаторы, что уже перечислены в [PROJECT_ANALYSIS.md, раздел 6 и 13.1](PROJECT_ANALYSIS.md); новые ID не изобретаются без необходимости.
- **Ending ID** — `END_01_COLLAPSE`, `END_02_SUCCESSOR`, `END_03_ESCAPE`, `END_04_BALANCE` — зафиксированы, не переименовываются.
- **Флаги** — `snake_case`, ровно как в [PROJECT_ANALYSIS.md, раздел 10](PROJECT_ANALYSIS.md) (`shard_obtained`, `father_file_found` и т.д.) — имена флагов из сюжетных документов копируются в JSON буквально, не адаптируются под C#-конвенции.
- **C#-классы/файлы** — `PascalCase`, один публичный класс на файл, имя файла = имя класса.

## 5. Сборка

Решение (`ZeroFloor.slnx`) и проект (`ZeroFloor.csproj`) не менялись физически — новые папки подхватываются SDK-style проектом автоматически. После добавления в них C#/XAML-файлов на следующих этапах обязательна проверка `dotnet build` (правило зафиксировано в [CLAUDE.md](CLAUDE.md)).
