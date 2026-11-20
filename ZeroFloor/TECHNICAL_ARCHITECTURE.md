# TECHNICAL_ARCHITECTURE.md — «Нулевой этаж»

> Основа: [PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md), `docs/Zero_Floor_Story_Bible_v2.1.docx`, `docs/Zero_Floor_Flowchart_v2.1.docx`.
> Технологии: **C# · WPF · .NET 10 · Visual Studio 2026**.
> Это документ архитектуры и границ ответственности компонентов. Игровой контент (тексты глав, сами 12 JSON-сценариев, финальные ассеты) в этом документе не создаётся — только структура, которая его примет.

---

## 1. Принципы архитектуры

1. **Контент отделён от кода.** Сюжет, реплики, выборы, условия и эффекты живут в JSON (`Data/Scenario`), а не в C#. Добавление/правка главы — это правка данных, а не пересборка движка.
2. **MVVM — только там, где есть UI.** Views (XAML) ↔ ViewModels (состояние экрана, команды) ↔ Models (данные). Движок (`Engine`) и сервисы (`Services`) — обычные C#-классы без `INotifyPropertyChanged` и без ссылок на XAML; ViewModel — единственный слой, который их вызывает и оборачивает результат в bindable-свойства. Не оборачивать в MVVM то, что не является экраном (например, `ConditionEvaluator` — чистая функция, ей ViewModel не нужен).
3. **Движок не знает про WPF.** `Engine` и `Models` не ссылаются на `System.Windows.Media`/`Controls`. Анимации и визуальные эффекты — отдельный слой (`Services/Presentation`), который получает команды от движка через события/интерфейсы, а WPF-специфику (Storyboard, RenderTransform) держит сам.
4. **Всё — через интерфейсы.** `ISaveService`, `IAssetService`, `IAudioService`, `ISettingsService`, `IScenarioRepository`, `INavigationService`, `IDialogueLogService` и т.д. ViewModels и Engine зависят от интерфейсов, не от конкретных классов → любую реализацию можно заменить без переписывания вызывающего кода (требование №11 — расширяемость).
5. **Одна точка правды для правил.** Пороги концовок (K≥40, father_file_found, H≥70, sonya_exposed), флаги и переменные — то, что уже зафиксировано в [PROJECT_ANALYSIS.md, разделы 7, 8, 10](PROJECT_ANALYSIS.md). Архитектура не вводит новые правила и не меняет эти условия — она только их исполняет.
6. **Один персонаж = одно изображение.** Никакой системы «выражений лица» или кадров анимации. Всё эмоциональное/динамическое воздействие достигается программно (WPF `Storyboard`/`Transform`/`Effect`) над одним и тем же `BitmapImage`. См. раздел 7.

---

## 2. Слои и зависимости

```
                    ┌─────────────┐
                    │    Views    │  XAML, минимум code-behind
                    └──────┬──────┘
                           │ DataContext / Binding / Command
                    ┌──────▼──────┐
                    │ ViewModels  │  состояние экрана, команды, bindable-свойства
                    └──────┬──────┘
             ┌─────────────┼─────────────────┐
             │             │                 │
      ┌──────▼─────┐ ┌─────▼──────┐   ┌──────▼───────┐
      │   Engine   │ │  Services  │   │    Models    │  POCO: GameState,
      │ (игровые   │ │ (сохранения,│   │  (структуры  │  SceneDefinition,
      │  правила)  │ │  ассеты,   │   │   данных)    │  DialogueNode,
      └──────┬─────┘ │  аудио,    │   └──────────────┘  Effect, Condition...
             │        │  настройки)│
             │        └─────┬──────┘
             │              │
             └──────┬───────┘
                     │
              ┌──────▼──────┐
              │  Data/Assets │  JSON-сценарии, изображения, звук (файлы на диске)
              └─────────────┘
```

Правило направления зависимостей: **Views → ViewModels → (Engine, Services) → Models**. Обратных ссылок нет (Models ничего не знают про Engine; Engine ничего не знает про ViewModels/Views). `Common` (базовые классы MVVM-инфраструктуры: `ViewModelBase`, `RelayCommand`, слабый мессенджер событий) используется Views/ViewModels и не зависит ни от Engine, ни от Services.

---

## 3. Модели данных (`Models`)

Чистые данные (POCO), без логики WPF. Полный список полей согласован с [PROJECT_ANALYSIS.md, раздел 13.1](PROJECT_ANALYSIS.md) и примером JSON из Story Bible (раздел E).

| Класс | Назначение |
|---|---|
| `GameState` | K, L, R, H (int, 0–100), словарь флагов (`IReadOnlyDictionary<string, FlagValue>`), текущие `chapterId`/`sceneId`/`nodeId`, `currentEndingId`, `currentBranchId`, множество `appliedChoiceIds`, список `viewedNodeIds` (для журнала/skip). |
| `FlagValue` | Значение флага: bool, int или string-вариант (нужно для `underground_game_outcome`, который не чисто bool). Одна структура вместо трёх типов флагов — проще сериализовать и валидировать. |
| `StatId` | Enum `K, L, R, H` — чтобы эффекты и условия не оперировали «магическими строками» для шкал (для флагов строки остаются, так как их список открыт и растёт по сюжету). |
| `SceneDefinition` | `sceneId`, `background`, `music`, `ambience`, список `DialogueNode`. Один файл JSON = одна глава (или одна подветка, напр. `Ch11B`), содержащая одну или несколько `SceneDefinition`. |
| `DialogueNode` | `nodeId`, `speaker` (characterId или `narrator`), `text`, список `ChoiceDefinition` (может быть пуст), `effects` (применяются при входе в узел — напр. побочные +H), `nextNodeId`/`nextSceneId`. |
| `ChoiceDefinition` | `choiceId`, `text`, `condition` (может быть `null` = всегда показывается), `effects`, `nextNodeId`/`nextSceneId`/`endingId`. |
| `ConditionNode` | Дерево условий: `Comparison(statId, op, value)`, `FlagEquals(flagKey, value)`, `And(children)`, `Or(children)`, `Not(child)`. **Осознанное архитектурное решение:** вместо свободного текстового выражения (как иллюстративный псевдокод в Flowchart, раздел 6) используется типизированное дерево — его нельзя написать с опечаткой в операторе, и его тривиально покрыть юнит-тестами per-условие. Игровая логика условий (K≥40, father_file_found и т.д.) не меняется — меняется только формат её записи в JSON. |
| `Effect` | `{ type, key, value }` — ровно так, как показано в примере JSON Story Bible (`setFlag`, `addStat`). Типы эффектов — закрытый, явно перечисленный набор (`SetFlag`, `AddStat`, `SetStat`); новый тип эффекта добавляется явным расширением `EffectApplier`, а не произвольной строкой. |
| `CharacterDefinition` | `characterId`, отображаемое имя, путь к единственному изображению, точка привязки (anchor) для анимаций, базовая позиция по умолчанию. |
| `EndingDefinition` | `endingId` (`END_01_COLLAPSE`…`END_04_BALANCE`), `branchId` (A–D), ссылка на стартовую сцену Главы 11 для этой ветки. Используется только для навигации/справки — сами условия доступности веток лежат в `condition` конкретного выбора Gate10, а не продублированы здесь. |
| `SaveSlotData` | `saveVersion`, `timestamp`, `playTimeSeconds`, `chapterId/sceneId/nodeId`, `currentEndingId`, `GameState`, история просмотренных узлов — поля ровно по [PROJECT_ANALYSIS.md, раздел 13.4](PROJECT_ANALYSIS.md). |
| `AppSettings` | Пять категорий громкости (Master/Music/SFX/Ambience/Voice), уровень анимаций, скорость текста, режим окна, масштаб UI, флаг «разрешить пропуск непрочитанного» (по умолчанию выключен). |

---

## 4. Движок (`Engine`) — реализовано

Чистая игровая логика, без WPF-зависимостей, покрыта 101 тестом в `ZeroFloor.Tests`. Каждый класс — маленький, с одной ответственностью.

| Компонент | Ответственность |
|---|---|
| `GameState` *(Models)* | Сама и есть единственный владелец своих K/L/R/H и флагов — `SetStat`/`AddToStat` (с clamp 0–100), `SetFlag`/`GetFlag`, `AppliedChoiceIds`, `ViewedNodeIds`. Отдельный `GameStateManager`-обёртка не понадобилась: мутация и так сосредоточена в одном месте (в самих методах `GameState`), а не размазана — лишняя обёртка вокруг единственного кирпичика дублировала бы её без выгоды. |
| `ScenarioRepository` *(в Services, вызывается движком)* | Загружает JSON (`Services/ScenarioRepository.cs`), сразу прогоняет через `ScenarioValidator` — ошибка сценарных данных обнаруживается при загрузке, не посреди прохождения. |
| `SceneManager` | Получает `SceneDefinition` по id через `IScenarioRepository`, определяет входной узел сцены (по соглашению — первый в списке `Nodes`). Защита от неизвестного id сцены/сцены без узлов. |
| `DialogueManager` | Держит указатель на текущую сцену/узел, применяет узловые `effects` при входе, валидирует переход на `nodeId` (существование, наличие текста/говорящего) перед тем как сделать его текущим. |
| `ConditionEvaluator` | Вычисляет `ConditionNode` (`>`, `>=`, `<`, `<=`, `=`, `and`, `or`, `not`, проверка флага с значением по умолчанию для ещё не установленного флага) над `GameState`. Чистая функция, без побочных эффектов. |
| `EffectApplier` | Применяет `setFlag`/`addStat`/`setStat` к `GameState`; неизвестный ключ шкалы или отсутствующее значение — `ScenarioValidationException`. |
| `ChoiceProcessor` | Фильтрует выборы узла по `ConditionEvaluator`; применяет эффекты выбранного варианта **ровно один раз** (идемпотентность через `GameState.AppliedChoiceIds` — повторный вызов для уже применённого `choiceId` не удваивает эффект, но корректно возвращает переход); требует ровно одно направление (`nextNodeId` XOR `nextSceneId` XOR `endingId`). |
| `EndingManager` | Единственный источник правды для Gate10: 4 канонические ветки (A всегда; B — K≥40; C — father_file_found; D — H≥70 И father_file_found И !sonya_exposed), жёстко заданные в коде по PROJECT_ANALYSIS.md/CLAUDE.md. `ResolveEnding` фиксирует `CurrentEndingId`/`CurrentBranchId` один раз — повторный вызов бросает исключение. |
| `GameSessionController` | Координатор: `Start(sceneId)` → `GetAvailableChoices()`/`Advance()`/`SelectChoice(id)` → (сцена/узел меняются ИЛИ фиксируется концовка). Единственный класс, который должен знать «что из движка когда вызывать» — будущий `NovelViewModel` обращается только к нему. |
| `ScenarioValidator` | Эагерная (load-time) проверка целостности всего сценария: дубли id сцен/узлов/выборов, висячие `nextNodeId`/`nextSceneId`, отсутствие текста/говорящего, структурно некорректные условия/эффекты. Дополняет (не заменяет) защитные проверки времени выполнения выше — тот же инвариант проверен на двух этапах жизненного цикла. |
| `ScenarioValidationException` | Единое исключение для всех нарушений целостности сценария — и при загрузке, и во время игры. |

---

## 5. Сервисы (`Services`)

Инфраструктурные возможности, за интерфейсами, подключаются в `App.xaml.cs` (композиционный корень).

| Интерфейс / реализация | Ответственность |
|---|---|
| `IScenarioRepository` / `ScenarioRepository` — **реализовано** | Загружает `SceneDefinition[]` из JSON-файлов (каждый файл — массив сцен), сразу валидирует через `ScenarioValidator`. Формат JSON и конвертеры — `Services/Json/` (`FlagValueJsonConverter`, `EffectJsonConverter`, `ComparisonOperatorJsonConverter` — символьные `>`,`>=`,`<`,`<=`,`=`; `ConditionNode` полиморфен по `[JsonDerivedType]` с дискриминатором `type`). |
| `ISaveService` / `SaveService` | ≥10 ручных слотов + автосейв. Атомарная запись (временный файл → переименование). Версионирование (`saveVersion`) с хуком миграции на будущее. Не перезаписывает последний ручной слот автосейвом. Повреждённый слот не ломает запуск игры — ошибка слота изолируется от списка остальных слотов. |
| `IAssetService` / `AssetService` | Резолвит `characterId`/`backgroundId`/`cgId` → `BitmapImage`, с кэшем по ключу (не грузить один и тот же PNG повторно). При отсутствующем файле возвращает явно помеченный «ресурс не найден» результат (напр. пурпурно-чёрная заглушка с текстом ID) — **никогда** не подставляет случайное стоковое изображение вместо отсутствующего (жёсткое правило, см. [CLAUDE.md](CLAUDE.md)). |
| `IAudioService` / `AudioService` **(реализовано)** — `Services/Presentation/` | Пять категорий громкости (Master × Music/SFX/Ambience/Voice). Музыка и эмбиент — по каналу `AudioChannel` (два `MediaPlayer` на кроссфейд, зацикливание через `MediaEnded`); SFX — пул одноразовых `MediaPlayer` (до 8 одновременно); голос — один `MediaPlayer`, всегда останавливает предыдущую реплику, `ReplayVoice()` для повтора. Плавные fade — `VolumeFader` (ручной tween, т.к. `MediaPlayer.Volume` не DependencyProperty и не анимируется Storyboard). Реагирует на `ISettingsStore.Changed` в реальном времени. Методы принимают только id (не `FrameworkElement`) — в отличие от `ICharacterAnimationService`/`ISceneTransitionService`, доступен ViewModel напрямую через `AppServices.Audio`. Технологическая совместимость: `System.Windows.Media.MediaPlayer` — часть PresentationCore, доступна без доп. NuGet-пакетов на net10.0-windows+UseWPF; единственное ограничение — объект thread-affine (DispatcherObject), поэтому создаётся/используется/освобождается на одном потоке (в приложении — поток UI, см. `App.xaml.cs`). |
| `AudioResourceResolver` **(реализовано, internal)** | Система именования: `Assets/Audio/<Category>/<resourceId>.<wav\|mp3\|wma>`, `resourceId` может содержать "/" для подпапок (напр. `max/ch01_n002` для голоса). Нет файла → `null`, без исключения — вызывающий код (`AudioService`) просто ничего не играет (CLAUDE.md: не подменять случайным звуком). |
| `ISettingsService` / `SettingsService` | Чтение/запись `AppSettings` в пользовательский конфиг-файл. Применяется requirement №10 (настройки, скорость текста). |
| `IDialogueLogService` / `DialogueLogService` | Накопление показанных реплик текущего прохождения (для экрана «журнал»). Явно **не** имеет доступа на запись в `GameState` — только читает поток уже показанных узлов (чтобы не могла случайно повлиять на игровую логику, как и требует PROJECT_ANALYSIS раздел 13.3). |
| `INavigationService` / `NavigationService` | Переключение текущего экрана (`MainMenu` ↔ `Novel` ↔ `SaveLoad` ↔ `Settings` ↔ `Gallery` ↔ `Archive`) без прямых ссылок одной ViewModel на другую. |
| `ILocalizationService` | Интерфейс-заглушка на будущее (Open Question №7 из PROJECT_ANALYSIS: нужна ли локализация — не решено). Текущая реализация по умолчанию — `PassthroughLocalizationService`, отдаёт строки как есть (русский текст из JSON). Добавление реального перевода не потребует менять ни `Engine`, ни `ViewModels` — только реализацию интерфейса. |
| `IGalleryService` / `IArchiveUnlockService` | Отслеживают, какие CG/фоны и архивные документы уже «увидены» игроком в текущем сохранении (для экранов Галерея/Архив из обязательных систем раздела 13.3). |
| `ILoggingService` / `FileLoggingService` | Запись необработанных исключений и диагностических сообщений в файл (`%LOCALAPPDATA%\ZeroFloor\logs`). Единственный сервис, который подключается до остальной композиции — глобальные обработчики ошибок в `App.xaml.cs` должны уметь логировать прежде, чем настроена навигация. |

---

## 6. ViewModels и Views

MVVM применяется строго к UI-слою:

| View (XAML) | ViewModel | Назначение |
|---|---|---|
| `MainMenuView` | `MainMenuViewModel` | Новая игра / продолжить / загрузить / настройки / выход. |
| `NovelView` | `NovelViewModel` | Главный экран новеллы: фон, слой персонажей, окно реплики, список выборов, кнопки журнала/сохранения/настроек/пропуска. Делегирует всё в `GameSessionController` — сама не содержит игровых правил. |
| `SaveLoadView` | `SaveLoadViewModel` | Список слотов, сохранение/загрузка/удаление слота через `ISaveService`. |
| `SettingsView` | `SettingsViewModel` | Биндинги к `AppSettings` через `ISettingsService`. |
| `GalleryView` | `GalleryViewModel` | Просмотр открытых CG/фонов через `IGalleryService`. |
| `ArchiveView` | `ArchiveViewModel` | Просмотр открытых архивных документов через `IArchiveUnlockService`. |
| `Views/Controls/DialogueBoxControl` | — (bind напрямую к `NovelViewModel`) | Повторно используемый UserControl: имя говорящего, текст (с посимвольной анимацией через `ITextRevealController`), индикатор продолжения. |
| `Views/Controls/ChoiceListControl` | — | Список доступных вариантов выбора, биндится к коллекции из `ChoiceProcessor`. |
| `Views/Controls/CharacterLayerControl` | — | Слой персонажей сцены; см. раздел 7 (анимация). |

`Common/ViewModelBase.cs` — базовый класс с `INotifyPropertyChanged`. `Common/RelayCommand.cs` — реализация `ICommand` без сторонних пакетов (решение: не тащить `CommunityToolkit.Mvvm`/`Prism` на старте ради минимальных зависимостей; при необходимости такой пакет можно подключить позже — ViewModels от конкретной MVVM-библиотеки не зависят, зависят только от `Common`).

---

## 7. Программная анимация персонажей (требование №7)

Жёсткое ограничение проекта: **один персонаж = одно статичное изображение** (обычная стоячая поза). Никаких слоёв лица, доп. поз или кадров — художник их не поставляет, и движок не должен их ожидать.

Вся «жизнь» персонажа на экране — чисто программные WPF-трансформации над этим единственным `Image`:

| Эффект | Как достигается | Когда используется |
|---|---|---|
| Появление / исчезновение | `DoubleAnimation` по `Opacity` (+ при необходимости `TranslateTransform` для скольжения) | Вход/выход персонажа из сцены |
| Смена позиции (лево/центр/право) | `TranslateTransform`/`Grid.Column` + анимация перехода между ними | Персонаж «подходит ближе», уступает место другому |
| Идle-дыхание/покачивание | Зацикленный `DoubleAnimation` c `AutoReverse=true`, `RepeatBehavior=Forever`, малая амплитуда по `ScaleTransform`/`TranslateTransform.Y` | Постоянный фон для «живости» статичного портрета |
| Акцент говорящего | Небольшой пульс `ScaleTransform` или усиление яркости (`BitmapEffect`/`Brightness`-подобный `Effect`) синхронно с выводом текста | Текущий `speaker` узла подсвечивается относительно остальных |
| Напряжение/дрожь | Короткая высокочастотная `DoubleAnimation` по X с `AutoReverse` | Сцены погони/допроса — усиление тона без нового изображения |
| Затемнение/силуэт | `Effect`/наложение полупрозрачного `Rectangle` над тем же `Image` | Напр. отец в тени до «раскрытия» — не требует второго изображения |

Архитектурно это реализует `Services/Presentation/ICharacterAnimationService` (WPF-зависимый, живёт **вне** `Engine`): принимает команды вида «проиграть анимацию X для персонажа Y» (команды решает `NovelViewModel`/сценарные данные — напр. поле `animationHint` в `DialogueNode`, необязательное и не влияющее на `GameState`), сам строит и запускает `Storyboard` на `CharacterLayerControl`. Движок не умеет и не должен уметь строить `Storyboard` — иначе он перестанет быть переносимым и тестируемым без WPF-окна.

То же самое решение (одна и та же техника) используется для `ISceneTransitionService` — программные переходы между фонами сцен (fade/cross-fade), чтобы не требовать от художника отдельных «переходных» ассетов.

---

## 8. Загрузка изображений, музыки и звуков (требование №8)

- `IAssetService` резолвит логический `id` (из JSON: `characterId`, `background`, `cgId`) в файл внутри `Assets/Backgrounds`, `Assets/Characters`, `Assets/CG` — физический путь никогда не хардкодится в `Engine` или `ViewModels`, только в реализации сервиса/манифесте.
- Кэш загруженных `BitmapImage` по `id`, с опцией выгрузки неиспользуемых при смене главы (контроль памяти на большом объёме фонов/CG).
- `IAudioService` аналогично резолвит `music`/`ambience`/`sfxId` в файлы `Assets/Audio/Music|Ambience|SFX`.
- Оба сервиса используют **один и тот же** принцип «явный manifest/ID → файл», что и обеспечивает требование №11 (расширение без переписывания): новый фон/трек — это новая запись в данных + новый файл, без изменения кода сервисов.
- Отсутствующий ассет **не** приводит к падению игры и **не** подменяется случайным файлом — сервис возвращает чётко обозначенный fallback и пишет предупреждение в лог разработки (требование CLAUDE.md «не заменять реальные ресурсы случайными файлами»).

---

## 9. Сохранения и загрузка (требование №9)

Полная спецификация полей и правил — [PROJECT_ANALYSIS.md, раздел 13.4](PROJECT_ANALYSIS.md); здесь — как это разложено по компонентам:

- `GameSessionController` просит `ISaveService.Save(slot)` в двух точках автосейва (вход в главу, перед Выбором 10) и по явной команде игрока (`SaveLoadViewModel`).
- `SaveSlotData` сериализуется целиком из текущего `GameState` + позиции (`chapterId/sceneId/nodeId`) + `currentEndingId`.
- Атомарность: запись во временный файл рядом со слотом, затем атомарная замена — повреждение при записи не портит существующий слот.
- При загрузке: `ChoiceProcessor`/`EndingManager` **не переигрывают** уже применённые эффекты — `appliedChoiceIds` восстанавливается из сейва первым, до того как игрок может сделать новый выбор.
- Несовпадение `saveVersion` → явное сообщение пользователю, а не молчаливая порча прогресса (хук миграции оставлен пустым до появления второй версии формата).

---

## 10. Настройки, журнал и скорость текста (требование №10)

- `AppSettings` (Models) хранит: громкость музыки/SFX/эмбиента, скорость текста (символов в секунду или пресет «медленно/нормально/быстро/мгновенно»), режим окна/масштаб UI, разрешён ли пропуск непрочитанного (по умолчанию — нет).
- `ITextRevealController` (Services/Presentation) — печатная машинка для `text` текущего узла, читает скорость из `ISettingsService`, поддерживает мгновенный вывод по клику/клавише и останавливается перед показом выборов.
- `IDialogueLogService` копит уже показанные узлы (только для чтения, экран «Журнал»); не путать с `appliedChoiceIds` в `GameState` — журнал про UI-историю, `GameState` про игровые факты.

---

## 11. Расширяемость без переписывания (требование №11)

Как архитектура конкретно обеспечивает это:

1. **Новая глава/сцена** — новый `*.json` в `Data/Scenario`, без изменений в `Engine`.
2. **Новый флаг/новая проверка существующего типа** (`setFlag`/`addStat` на новую шкалу-ключ) — правится только JSON; `EffectApplier`/`ConditionEvaluator` уже умеют работать с любым ключом.
3. **Новый персонаж** — новая запись `CharacterDefinition` (`Data/Characters`) + один файл изображения в `Assets/Characters`; `CharacterLayerControl`/`ICharacterAnimationService` уже рассчитаны на произвольный список персонажей.
4. **Новый экран** (напр. «Титры») — новая пара View/ViewModel + точка в `INavigationService`; существующие экраны не трогаются.
5. **Новая реализация сервиса** (напр. облачные сохранения вместо локальных файлов) — новый класс за тем же `ISaveService`, композиционный корень (`App.xaml.cs`) меняет одну строку регистрации.
6. **Что НЕ расширяется без отдельного решения сценариста**: список и условия 4 концовок, количество глав, смысл флагов из [PROJECT_ANALYSIS.md, раздел 10](PROJECT_ANALYSIS.md) — это зафиксировано как контент, а не как техническое ограничение; правило закреплено в [CLAUDE.md](CLAUDE.md), а не в коде, потому что код технически способен выразить и пятую концовку — делать этого без разрешения нельзя.

---

## 12. Список основных компонентов и их ответственности (сводно)

| Компонент | Слой | Ответственность одной строкой |
|---|---|---|
| `GameState`, `FlagValue`, `SceneDefinition`, `DialogueNode`, `ChoiceDefinition`, `ConditionNode`, `Effect`, `CharacterDefinition`, `EndingDefinition`, `SaveSlotData`, `AppSettings` | Models | Чистые данные игры, сцены, сохранения, настроек — без логики. |
| `SceneManager` **(реализовано)** | Engine | Навигация между сценами по графу `nextSceneId`. |
| `DialogueManager` **(реализовано)** | Engine | Навигация между узлами внутри сцены, применение узловых эффектов. |
| `ConditionEvaluator` **(реализовано)** | Engine | Вычисление дерева условий над `GameState`. |
| `EffectApplier` **(реализовано)** | Engine | Применение `Effect` к `GameState`. |
| `ChoiceProcessor` **(реализовано)** | Engine | Фильтрация доступных выборов, идемпотентное применение одного выбора. |
| `EndingManager` **(реализовано)** | Engine | Расчёт доступных веток Gate10, фиксация `currentEndingId` без пересчёта. |
| `GameSessionController` **(реализовано)** | Engine | Координация сцены/диалога/выбора как единого потока событий для ViewModel (автосейв — после подключения `SaveService`). |
| `ScenarioValidator` / `ScenarioValidationException` **(реализовано)** | Engine | Эагерная проверка целостности сценария при загрузке; единое исключение для всех нарушений. |
| `ScenarioRepository` **(реализовано)** | Services | Загрузка, валидация и кэш JSON-сценариев. |
| `SaveService` | Services | Слоты сохранений, атомарная запись, версия формата. |
| `AssetService` | Services | Резолв и кэш изображений по ID, безопасный fallback. |
| `AudioService` **(реализовано)** | Services/Presentation | Музыка/эмбиент/SFX/голос, 5 категорий громкости, кроссфейд, реакция на настройки в реальном времени. |
| `SettingsService` | Services | Чтение/запись `AppSettings`. |
| `DialogueLogService` | Services | История показанных реплик (только чтение для UI). |
| `NavigationService` | Services | Переключение текущего экрана. |
| `LocalizationService` | Services | Точка расширения на будущий перевод строк (сейчас passthrough). |
| `GalleryService` / `ArchiveUnlockService` | Services | Учёт открытых CG/фонов/документов. |
| `FileLoggingService` | Services | Запись исключений/диагностики в файл для разбора после краша или бага. |
| `ICharacterAnimationService` / `CharacterAnimationService` **(реализовано)** | Services/Presentation (WPF) | Enter/Exit/MoveTo/ScaleTo/idle-bob персонажа над одним изображением — Opacity+TranslateTransform+ScaleTransform, без кадров. |
| `ISceneTransitionService` / `SceneTransitionService` **(реализовано)** | Services/Presentation (WPF) | Кроссфейд фонов, лёгкий зум камеры (сцена целиком), fade to/from black, параллакс. |
| `IUiRevealService` / `UiRevealService` **(реализовано)** | Services/Presentation (WPF) | Единообразное плавное появление панелей интерфейса (окно диалога, список выбора) при показе. |
| `ITextRevealController` | Services/Presentation (WPF) | Печатная машинка текста со скоростью из настроек — ещё не реализовано. |
| `AnimationPrimitives` / `StageLayout` **(реализовано)** | Services/Presentation (internal) | Общая "сантехника" Storyboard+TaskCompletionSource и шкалирование длительности по `AppSettings.AnimationLevel`; раскладка именованных позиций сцены в пиксели. |
| `MainMenuViewModel`…`ArchiveViewModel` | ViewModels | Bindable-состояние и команды каждого экрана. |
| `ViewModelBase`, `RelayCommand` | Common | Базовая MVVM-инфраструктура без внешних пакетов. |

---

## 12a. Интерактивные 3D-эпизоды (Главы 2А и 6)

- **Данные** — `MinigameCue.Kind = MarketStealth3D | ShaftsChase3D` + `Episode3DLayout` (маршрут → `choiceId` того же узла, исход → эффекты `setFlag mg_*`, исход → узел той же сцены). Валидируются `ScenarioValidator.ValidateEpisode3D` при загрузке.
- **Движок** — новых правил нет. Выбор маршрута идёт через обычный `GameSessionController.SelectChoice` (идемпотентность — `ChoiceProcessor`). Исход — `GameSessionController.ApplyMinigameOutcomeEffects`, который через `EffectApplier` разрешает `setFlag` (любой флаг) и `addStat` в пределах ±10 (`MaxMinigameStatDelta`); `setStat` запрещён.
- **ViewModel** — `Episode3DViewModel` (мост: `CommitRoute`/`Complete` ровно один раз + настройки паузы). `NovelViewModel.ActiveEpisode` — тот же слот, что у прочих мини-игр; варианты выбора узла при эпизоде списком не показываются.
- **Presentation** — `Services/Presentation/World3D`: всё 3D (WPF `Viewport3D`, без внешних пакетов), процедурные модели/текстуры/анимации/звуки. `Views/Controls/Episode3DOverlayControl` — Viewport + HUD + пауза; возврат к новелле — затемнение эпизода и проявление `NovelView` из чёрного.
- **Глава 8** — `ArchiveStealth3D` (эпизод без маршрутов: после исхода сюжет идёт по `nextNodeId` узла), `DossierAssembly` (2D, `DossierAssemblyViewModel` + `DossierAssemblyOverlayControl`: результат выбирает существующий вариант узла) и «жёсткий режим» `CodeAssemblyLayout.HardModeFlag` (параметры строже, если поднят флаг).
- **Модель Макса** — `PortraitSkin` проецирует авторский портрет на тело гуманоида (режимы Body/Arm/Head); тыл и ноги — фрагменты ткани и волос с того же портрета.
- **Настройки** — `AppSettings.EpisodeMouseSensitivity / EpisodeInvertY / EpisodeCameraShake / EpisodeShowControlHints`; тряска камеры учитывает `AnimationLevel`.

---

## 13. План этапов разработки

Этапы соответствуют производственному плану [PROJECT_ANALYSIS.md, раздел 13.6](PROJECT_ANALYSIS.md), но детализированы до конкретных компонентов этой архитектуры.

| Этап | Что делается | Готово, когда |
|---|---|---|
| **0. Архитектура и документация** (этот этап) | `CLAUDE.md`, `TECHNICAL_ARCHITECTURE.md`, `PROJECT_STRUCTURE.md`, структура каталогов. | Документы и папки созданы; решение не блокирует начало этапа 1. |
| **1. Вертикальный срез** *(частично: UI-основа и навигация готовы отдельным проходом, движок — этапом 2 ниже)* | `Models`, `ScenarioRepository`, `SceneManager`+`DialogueManager`, `NovelViewModel`+`NovelView` с демо-данными, `SaveService`. | Главное меню → сцена → выбор → переход → сохранение/загрузка работают на реальном JSON. |
| **2. Движок** — ✅ **выполнено** | `ConditionEvaluator` (`>`,`>=`,`<`,`<=`,`=`,`and`,`or`,`not`, проверка флага), `EffectApplier` (`setFlag`/`addStat`/`setStat`), `ChoiceProcessor` с идемпотентностью, `EndingManager` с 4 каноническими ветками Gate10, `ScenarioValidator`, JSON-формат сценария (`Services/Json/`). | ✅ 101 тест в `ZeroFloor.Tests` проходит (`dotnet test`), включая аналоги T07–T11 из [PROJECT_ANALYSIS.md, раздел 13.5](PROJECT_ANALYSIS.md) на реальных канонических условиях Gate10. |
| **3. Интерфейс** | `DialogueBoxControl`, `ChoiceListControl`, `CharacterLayerControl` (с анимациями из раздела 7), `SettingsView`, журнал реплик, пропуск/авточтение. | Читать и выбирать удобно мышью и клавиатурой; настройки влияют на текст/звук в реальном времени. |
| **4. Глава 1 целиком** | `CH01_LOW_FLOW.json` по формату раздела 3, все фоны/звуки через `AssetService`/`AudioService`. | Глава 1 полностью проходима от меню до перехода в Главу 2, без ручных подпорок в коде. |
| **5. Ветвящийся прототип** | Главы 8–11 (все 4 версии 11.A–11.D) + Gate10, `EndingManager` на реальных условиях. | Все 4 концовки достижимы по тестовым маршрутам из PROJECT_ANALYSIS, раздел 9.4. |
| **6. Полный перенос** | Все 12 глав, Галерея, Архив, полный набор ассетов. | Нет пустых ссылок (`nextNodeId`/`nextSceneId` в никуда) и отсутствующих ресурсов — проверяется валидацией `ScenarioRepository` при загрузке. |
| **7. QA и сборка** | Прогон тест-плана (включая T12 — повторная загрузка сейва), проверка производительности, релизная сборка Windows 10/11. | Сборка запускается на чистом профиле; каждая из 4 концовок пройдена вручную минимум один раз. |

---

## 14. Что этот документ сознательно не решает

Открытые вопросы из [PROJECT_ANALYSIS.md, раздел 14](PROJECT_ANALYSIS.md) (устаревшее условие K≥30, статус порога L≥70, тип `gosha_trust`, локализация и т.д.) — это вопросы контента/баланса, не архитектуры. Архитектура спроектирована так, чтобы ответ на любой из них менялся правкой JSON/конфигурации, а не кода (см. раздел 11). Технические детали UI-раскладки (конкретные XAML-макеты, палитра, типографика) не входят в этот документ — они задаются на этапе 3 отдельно от структуры кода.
