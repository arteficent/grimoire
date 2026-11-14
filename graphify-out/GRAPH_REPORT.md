# Graph Report - .  (2026-11-14)

## Corpus Check
- 71 files · ~118,038 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 894 nodes · 1605 edges · 62 communities (55 shown, 7 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 37 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Scene State Model|Scene State Model]]
- [[_COMMUNITY_Concurrency Scene Renderer|Concurrency Scene Renderer]]
- [[_COMMUNITY_Main View Model|Main View Model]]
- [[_COMMUNITY_Lesson Layout Kit|Lesson Layout Kit]]
- [[_COMMUNITY_Lock Primitive Comparison|Lock Primitive Comparison]]
- [[_COMMUNITY_Machine Learning Lessons|Machine Learning Lessons]]
- [[_COMMUNITY_Diagram Renderer|Diagram Renderer]]
- [[_COMMUNITY_Thread Pool Starvation|Thread Pool Starvation]]
- [[_COMMUNITY_Operating System Lessons|Operating System Lessons]]
- [[_COMMUNITY_Deadlock Watchdog|Deadlock Watchdog]]
- [[_COMMUNITY_Processor Lessons and Entry|Processor Lessons and Entry]]
- [[_COMMUNITY_Interface Protocol Lessons|Interface Protocol Lessons]]
- [[_COMMUNITY_Software Design Lessons|Software Design Lessons]]
- [[_COMMUNITY_System Design Lessons|System Design Lessons]]
- [[_COMMUNITY_Algorithm Lessons|Algorithm Lessons]]
- [[_COMMUNITY_Scenario Catalog|Scenario Catalog]]
- [[_COMMUNITY_Color Palette|Color Palette]]
- [[_COMMUNITY_Data Representation Lessons|Data Representation Lessons]]
- [[_COMMUNITY_GPU Lessons|GPU Lessons]]
- [[_COMMUNITY_Projects and Packages|Projects and Packages]]
- [[_COMMUNITY_Story Builder|Story Builder]]
- [[_COMMUNITY_Memory Lessons|Memory Lessons]]
- [[_COMMUNITY_Data Structure Lessons|Data Structure Lessons]]
- [[_COMMUNITY_Shared Resource Demo|Shared Resource Demo]]
- [[_COMMUNITY_Deadlock Stories|Deadlock Stories]]
- [[_COMMUNITY_Copy-on-Write Scenario|Copy-on-Write Scenario]]
- [[_COMMUNITY_Resource Management Lessons|Resource Management Lessons]]
- [[_COMMUNITY_Producer Consumer Scenario|Producer Consumer Scenario]]
- [[_COMMUNITY_Curriculum Tests|Curriculum Tests]]
- [[_COMMUNITY_Reader Writer Upgrade|Reader Writer Upgrade]]
- [[_COMMUNITY_Main Window Input|Main Window Input]]
- [[_COMMUNITY_Lost Update Scenario|Lost Update Scenario]]
- [[_COMMUNITY_Concurrent Collection Stories|Concurrent Collection Stories]]
- [[_COMMUNITY_Synchronization Stories|Synchronization Stories]]
- [[_COMMUNITY_Console Output Contract|Console Output Contract]]
- [[_COMMUNITY_Dictionary Corruption Scenario|Dictionary Corruption Scenario]]
- [[_COMMUNITY_Unsafe vs Safe Resource|Unsafe vs Safe Resource]]
- [[_COMMUNITY_Curriculum Registry|Curriculum Registry]]
- [[_COMMUNITY_Compound Operation Scenario|Compound Operation Scenario]]
- [[_COMMUNITY_Sync over Async Deadlock|Sync over Async Deadlock]]
- [[_COMMUNITY_Scenario Base|Scenario Base]]
- [[_COMMUNITY_Missed Pulse Deadlock|Missed Pulse Deadlock]]
- [[_COMMUNITY_Scenario Runner|Scenario Runner]]
- [[_COMMUNITY_Thread Helpers|Thread Helpers]]
- [[_COMMUNITY_Async Mutex Scenario|Async Mutex Scenario]]
- [[_COMMUNITY_Memory Visibility Scenario|Memory Visibility Scenario]]
- [[_COMMUNITY_Thread and Core Nodes|Thread and Core Nodes]]
- [[_COMMUNITY_Scenario Contract|Scenario Contract]]
- [[_COMMUNITY_Lock Ordering Deadlock|Lock Ordering Deadlock]]
- [[_COMMUNITY_Lock Ordering Fix|Lock Ordering Fix]]
- [[_COMMUNITY_Reentrancy Self Deadlock|Reentrancy Self Deadlock]]
- [[_COMMUNITY_TryEnter Backoff|TryEnter Backoff]]
- [[_COMMUNITY_Coordination Primitives|Coordination Primitives]]
- [[_COMMUNITY_Catalog Sync Test|Catalog Sync Test]]
- [[_COMMUNITY_Avalonia App Bootstrap|Avalonia App Bootstrap]]
- [[_COMMUNITY_App Version Info|App Version Info]]
- [[_COMMUNITY_Test Repo Paths|Test Repo Paths]]
- [[_COMMUNITY_Scenario Context|Scenario Context]]
- [[_COMMUNITY_Diagram Shapes|Diagram Shapes]]
- [[_COMMUNITY_Scenario Context Contract|Scenario Context Contract]]

## God Nodes (most connected - your core abstractions)
1. `SceneState` - 59 edges
2. `SceneCanvas` - 42 edges
3. `MainViewModel` - 35 edges
4. `ScenarioBase` - 22 edges
5. `Kit` - 18 edges
6. `AiLessons` - 17 edges
7. `InterfaceLessons` - 17 edges
8. `SoftwareDesignLessons` - 17 edges
9. `Lesson` - 16 edges
10. `SystemDesignLessons` - 16 edges

## Surprising Connections (you probably didn't know these)
- `CompoundOperationScenario` --inherits--> `ScenarioBase`  [EXTRACTED]
  grimoire/Live/Scenarios/DataStructures/CompoundOperationScenario.cs → grimoire/Live/Scenarios/ScenarioBase.cs
- `DictionaryCorruptionScenario` --inherits--> `ScenarioBase`  [EXTRACTED]
  grimoire/Live/Scenarios/DataStructures/DictionaryCorruptionScenario.cs → grimoire/Live/Scenarios/ScenarioBase.cs
- `ProducerConsumerScenario` --inherits--> `ScenarioBase`  [EXTRACTED]
  grimoire/Live/Scenarios/DataStructures/ProducerConsumerScenario.cs → grimoire/Live/Scenarios/ScenarioBase.cs
- `SnapshotAndCopyOnWriteScenario` --inherits--> `ScenarioBase`  [EXTRACTED]
  grimoire/Live/Scenarios/DataStructures/SnapshotAndCopyOnWriteScenario.cs → grimoire/Live/Scenarios/ScenarioBase.cs
- `UnsafeVsSafeResourceScenario` --inherits--> `ScenarioBase`  [EXTRACTED]
  grimoire/Live/Scenarios/DataStructures/UnsafeVsSafeResourceScenario.cs → grimoire/Live/Scenarios/ScenarioBase.cs

## Import Cycles
- None detected.

## Communities (62 total, 7 thin omitted)

### Community 0 - "Scene State Model"
Cohesion: 0.06
Nodes (12): CoreNode, Func, HashSet, Link, ResourceKind, ResourceNode, Shape, ThreadNode (+4 more)

### Community 1 - "Concurrency Scene Renderer"
Cohesion: 0.09
Nodes (29): AvaloniaPropertyChangedEventArgs, Control, FontFamily, FontWeight, FormattedText, bool, Color, CoreNode (+21 more)

### Community 2 - "Main View Model"
Cohesion: 0.09
Nodes (21): CancellationTokenSource, DispatcherTimer, bool, bool, double, int, List, SceneState (+13 more)

### Community 3 - "Lesson Layout Kit"
Cohesion: 0.10
Nodes (19): Color, Dictionary, double, IReadOnlyList, SceneState, Text, Tone, X (+11 more)

### Community 4 - "Lock Primitive Comparison"
Cohesion: 0.09
Nodes (15): CancellationToken, int, IScenarioContext, object, Task, ReaderWriterLockSlim, SemaphoreSlim, SpinLock (+7 more)

### Community 5 - "Machine Learning Lessons"
Cohesion: 0.17
Nodes (8): double, IEnumerable, Lesson, X, Y, Lesson, AiLessons, AiLessons

### Community 6 - "Diagram Renderer"
Cohesion: 0.23
Nodes (12): Border, Fill, Color, DrawingContext, Link, Point, Rect, SceneState (+4 more)

### Community 7 - "Thread Pool Starvation"
Cohesion: 0.09
Nodes (15): BoundedWorkerPool, ThreadPoolStarvationScenario, BlockingCollection, Task, TimeSpan, Action, BlockingCollection, CancellationToken (+7 more)

### Community 8 - "Operating System Lessons"
Cohesion: 0.15
Nodes (8): IEnumerable, Lesson, int, IBrush, IOutput, ObservableCollection, OperatingSystemLessons, UiOutput

### Community 9 - "Deadlock Watchdog"
Cohesion: 0.13
Nodes (16): IDeadlockWatchdog, Exception, Action, CancellationToken, Func, Task, TimeSpan, WatchResult (+8 more)

### Community 10 - "Processor Lessons and Entry"
Cohesion: 0.20
Nodes (6): AppBuilder, Program, IEnumerable, Lesson, STAThread, ArchitectureLessons

### Community 11 - "Interface Protocol Lessons"
Cohesion: 0.25
Nodes (3): IEnumerable, Lesson, InterfaceLessons

### Community 12 - "Software Design Lessons"
Cohesion: 0.26
Nodes (3): IEnumerable, Lesson, SoftwareDesignLessons

### Community 13 - "System Design Lessons"
Cohesion: 0.27
Nodes (3): IEnumerable, Lesson, SystemDesignLessons

### Community 14 - "Algorithm Lessons"
Cohesion: 0.25
Nodes (5): IEnumerable, Lesson, SceneState, string, AlgorithmLessons

### Community 15 - "Scenario Catalog"
Cohesion: 0.13
Nodes (11): IScenarioCatalog, IEnumerable, IGrouping, IScenario, ScenarioCategory, Dictionary, IEnumerable, IGrouping (+3 more)

### Community 16 - "Color Palette"
Cohesion: 0.19
Nodes (7): Color, Dictionary, ResourceKind, ThreadPhase, Tone, IImmutableSolidColorBrush, Palette

### Community 17 - "Data Representation Lessons"
Cohesion: 0.27
Nodes (5): IEnumerable, Lesson, SceneState, Tone, DataRepresentationLessons

### Community 18 - "GPU Lessons"
Cohesion: 0.26
Nodes (6): Func, IEnumerable, Lesson, SceneState, Tone, GpuLessons

### Community 19 - "Projects and Packages"
Cohesion: 0.13
Nodes (13): net10.0, Microsoft.NET.Sdk, Avalonia (11.3.23), Avalonia.Desktop (11.3.23), Avalonia.Fonts.Inter (11.3.23), Avalonia.Themes.Fluent (11.3.23), CommunityToolkit.Mvvm (8.4.2), coverlet.collector (6.0.4) (+5 more)

### Community 20 - "Story Builder"
Cohesion: 0.18
Nodes (7): Action, Lesson, List, SceneState, Story, string, StoryBuilder

### Community 21 - "Memory Lessons"
Cohesion: 0.32
Nodes (3): IEnumerable, Lesson, MemoryLessons

### Community 22 - "Data Structure Lessons"
Cohesion: 0.34
Nodes (3): IEnumerable, Lesson, DataStructureLessons

### Community 23 - "Shared Resource Demo"
Cohesion: 0.21
Nodes (9): ConcurrentDictionary, ConcurrentQueue, Dictionary, List, object, ISharedResource, ThreadSafeResource, ThreadUnsafeResource (+1 more)

### Community 24 - "Deadlock Stories"
Cohesion: 0.36
Nodes (3): IEnumerable, Story, DeadlockStories

### Community 25 - "Copy-on-Write Scenario"
Cohesion: 0.29
Nodes (6): CopyOnWriteStore, SnapshotAndCopyOnWriteScenario, CancellationToken, int, IScenarioContext, Task

### Community 26 - "Resource Management Lessons"
Cohesion: 0.38
Nodes (3): IEnumerable, Lesson, ResourceLessons

### Community 27 - "Producer Consumer Scenario"
Cohesion: 0.33
Nodes (5): ProducerConsumerScenario, CancellationToken, int, IScenarioContext, Task

### Community 28 - "Curriculum Tests"
Cohesion: 0.29
Nodes (3): CurriculumTests, Fact, IReadOnlyList

### Community 29 - "Reader Writer Upgrade"
Cohesion: 0.33
Nodes (5): ReaderWriterUpgradeDeadlockScenario, CancellationToken, int, IScenarioContext, Task

### Community 30 - "Main Window Input"
Cohesion: 0.20
Nodes (6): EventArgs, KeyEventArgs, MainViewModel, NotifyCollectionChangedEventArgs, MainWindow, Window

### Community 31 - "Lost Update Scenario"
Cohesion: 0.24
Nodes (7): Action, CancellationToken, Func, int, IScenarioContext, Task, LostUpdateScenario

### Community 32 - "Concurrent Collection Stories"
Cohesion: 0.42
Nodes (3): IEnumerable, Story, ConcurrentCollectionStories

### Community 33 - "Synchronization Stories"
Cohesion: 0.42
Nodes (3): IEnumerable, Story, SynchronizationStories

### Community 35 - "Dictionary Corruption Scenario"
Cohesion: 0.28
Nodes (6): DictionaryCorruptionScenario, CancellationToken, Func, int, IScenarioContext, Task

### Community 36 - "Unsafe vs Safe Resource"
Cohesion: 0.28
Nodes (6): UnsafeVsSafeResourceScenario, CancellationToken, int, IScenarioContext, Task, ISharedResource

### Community 37 - "Curriculum Registry"
Cohesion: 0.25
Nodes (6): IReadOnlyList, Lesson, List, Lazy, Curriculum, Track

### Community 38 - "Compound Operation Scenario"
Cohesion: 0.29
Nodes (5): CompoundOperationScenario, Action, CancellationToken, IScenarioContext, Task

### Community 39 - "Sync over Async Deadlock"
Cohesion: 0.39
Nodes (4): SyncOverAsyncDeadlockScenario, CancellationToken, IScenarioContext, Task

### Community 40 - "Scenario Base"
Cohesion: 0.39
Nodes (5): CancellationToken, IScenarioContext, Task, IScenario, ScenarioBase

### Community 41 - "Missed Pulse Deadlock"
Cohesion: 0.38
Nodes (4): MissedPulseDeadlockScenario, CancellationToken, IScenarioContext, Task

### Community 42 - "Scenario Runner"
Cohesion: 0.29
Nodes (5): CancellationToken, IScenario, IScenarioContext, Task, ScenarioRunner

### Community 43 - "Thread Helpers"
Cohesion: 0.29
Nodes (4): Action, Thread, TimeSpan, ThreadHelper

### Community 44 - "Async Mutex Scenario"
Cohesion: 0.33
Nodes (4): CancellationToken, IScenarioContext, Task, AsyncMutexScenario

### Community 45 - "Memory Visibility Scenario"
Cohesion: 0.29
Nodes (5): bool, CancellationToken, IScenarioContext, Task, MemoryVisibilityScenario

### Community 46 - "Thread and Core Nodes"
Cohesion: 0.29
Nodes (3): CoreNode, ResourceNode, ThreadNode

### Community 47 - "Scenario Contract"
Cohesion: 0.33
Nodes (4): IScenario, CancellationToken, IScenarioContext, Task

### Community 48 - "Lock Ordering Deadlock"
Cohesion: 0.33
Nodes (4): LockOrderingDeadlockScenario, CancellationToken, IScenarioContext, Task

### Community 49 - "Lock Ordering Fix"
Cohesion: 0.33
Nodes (4): LockOrderingFixScenario, CancellationToken, IScenarioContext, Task

### Community 50 - "Reentrancy Self Deadlock"
Cohesion: 0.33
Nodes (4): ReentrancySelfDeadlockScenario, CancellationToken, IScenarioContext, Task

### Community 51 - "TryEnter Backoff"
Cohesion: 0.33
Nodes (4): TryEnterBackoffScenario, CancellationToken, IScenarioContext, Task

### Community 52 - "Coordination Primitives"
Cohesion: 0.33
Nodes (4): CancellationToken, IScenarioContext, Task, CoordinationPrimitivesScenario

### Community 53 - "Catalog Sync Test"
Cohesion: 0.40
Nodes (3): CatalogTests, Fact, string

## Knowledge Gaps
- **198 isolated node(s):** `Action`, `Func`, `Task`, `IScenarioContext`, `CancellationToken` (+193 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ScenarioBase` connect `Scenario Base` to `Dictionary Corruption Scenario`, `Unsafe vs Safe Resource`, `Lock Primitive Comparison`, `Compound Operation Scenario`, `Sync over Async Deadlock`, `Thread Pool Starvation`, `Missed Pulse Deadlock`, `Async Mutex Scenario`, `Memory Visibility Scenario`, `Lock Ordering Deadlock`, `Lock Ordering Fix`, `Reentrancy Self Deadlock`, `TryEnter Backoff`, `Coordination Primitives`, `Copy-on-Write Scenario`, `Producer Consumer Scenario`, `Reader Writer Upgrade`, `Lost Update Scenario`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **Why does `PrimitiveComparisonScenario` connect `Lock Primitive Comparison` to `Scenario Base`?**
  _High betweenness centrality (0.015) - this node is a cross-community bridge._
- **What connects `Action`, `Func`, `Task` to the rest of the system?**
  _198 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Scene State Model` be split into smaller, more focused modules?**
  _Cohesion score 0.055533199195171024 - nodes in this community are weakly interconnected._
- **Should `Concurrency Scene Renderer` be split into smaller, more focused modules?**
  _Cohesion score 0.08680555555555555 - nodes in this community are weakly interconnected._
- **Should `Main View Model` be split into smaller, more focused modules?**
  _Cohesion score 0.08748615725359911 - nodes in this community are weakly interconnected._
- **Should `Lesson Layout Kit` be split into smaller, more focused modules?**
  _Cohesion score 0.10158730158730159 - nodes in this community are weakly interconnected._