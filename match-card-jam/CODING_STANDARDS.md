# Engineering & Coding Standards

This document establishes the official engineering, architectural, and code quality standards for the project. All contributors and AI agents must strictly adhere to these guidelines.

---

## 1. Language & Localization Policy (STRICT)

* **100% English Across All Codebase Assets**:
  * **Strictly Prohibited**: No Turkish or any other non-English language may appear in code files.
  * **Enforced Across**:
    * Class, struct, interface, enum, method, and variable names.
    * Code comments, XML docstrings (`/// <summary>`), and inline notes.
    * `Debug.Log`, `Debug.LogWarning`, and `Debug.LogError` messages.
    * Inspector attributes (`[Tooltip("...")]`, `[Header("...")]`, `[ContextMenu("...")]`).
    * ScriptableObject fields and enum string identifiers.
* **Why**: Ensures cross-team interoperability, industry-grade professionalism, seamless CI/CD integration, and git history cleanliness.

---

## 2. Code Quality & Architectural Principles

### 2.1 SOLID & Clean Architecture
* **Single Responsibility Principle (SRP)**: Each class must have one well-defined responsibility (e.g., audio playback logic must not leak into input processing; card animation must not manage game state).
* **Open/Closed Principle (OCP)**: Systems should be open for extension but closed for modification. Prefer polymorphism, interfaces, and ScriptableObject strategies over long switch-case blocks.
* **Loose Coupling via Events**:
  * Use C# `System.Action` / events for cross-system communication rather than direct hard references between managers.
  * Listeners must always unsubscribe (`-=`) in `OnDestroy()` (or an explicit `Dispose()`/`Cleanup()` method) to avoid memory leaks.

### 2.2 Flexibility & Extensibility
* Design components so new features (e.g., new card types, new power-ups, new level mechanics) can be added without rewriting existing systems.
* Decouple UI from core gameplay data: UI scripts should observe state changes and update visuals reactively.

---

## 3. Unity & C# Best Practices

### 3.1 Naming Conventions
| Element | Convention | Example |
| :--- | :--- | :--- |
| Classes / Structs / Interfaces | PascalCase (Interfaces prefixed with `I`) | `MatchItem`, `IDamageable` |
| Methods & Properties | PascalCase | `GetRemainingTime()`, `CurrentScore` |
| Public Events / Actions | PascalCase (Verb / Past tense) | `OnLevelCompleted`, `OnCardSelected` |
| Private / Protected Fields | `_camelCase` with leading underscore | `_moveDuration`, `_soundSource` |
| Serialized Private Fields | `[SerializeField] private Type _fieldName;` | `[SerializeField] private float _delay;` |
| Constants / Readonly Statics | UPPER_SNAKE_CASE or PascalCase | `MAX_LIVES`, `DefaultTimeout` |
| Local Variables & Parameters | camelCase | `targetPosition`, `elapsedTime` |

### 3.2 Inspector & Encapsulation
* **Never use public fields** purely for Inspector exposure. Always use `[SerializeField] private`.
* Group serialized fields logically using `[Header("...")]` and clarify behavior with `[Tooltip("...")]`.
* Validate required components and references using `[RequireComponent]` or assertion checks in `Awake()`.

### 3.3 Performance & Mobile Optimization
* **Avoid GC Allocations in Hot Paths**:
  * No `GetComponent<T>()` or `FindObjectOfType<T>()` inside `Update()`, `LateUpdate()`, or `FixedUpdate()`. Cache references in `Awake()` or `Start()`.
  * Avoid string concatenation or `string.Format` inside loops or `Update()`; use cached strings or `StringBuilder` when necessary.
* **Object Lifecycle & Tween Safety**:
  * When using tween engines (DOTween), always kill active tweens on the target object before starting new ones or upon `OnDestroy()` / `OnDisable()`.
  * Clean up coroutines, listeners, and dynamic subscriptions when GameObjects are recycled or destroyed.
* **Object Pooling**:
  * High-frequency GameObjects (particles, audio sources, floating UI indicators) must use pooling rather than runtime `Instantiate` and `Destroy`.

### 3.4 Deterministic Initialization & Lifecycle Management
* **Avoid Unnecessary Lifecycle Hooks**: Do not add `Awake()`, `Start()`, `OnEnable()`, or `OnDisable()` when initialization can be driven explicitly by a parent coordinator.
* **Top-Down Explicit Initialization (`Initialize`)**:
  * Scene-level subsystems (e.g., `PowerupManager`, `InputManager`, `TimerUIManager`, `ProgressionUIManager`) must be resolved and initialized explicitly by `GameManager` via an `Initialize(...)` method.
  * Dynamically spawned child items (e.g., `PowerupUiItem` spawned by `PowerupManager`) must be initialized explicitly by their spawning manager immediately after instantiation rather than relying on their own `Awake()`/`Start()`.
* **Event & Tween Cleanup**:
  * Subscribe to events inside `Initialize(...)` (ensuring idempotency by unsubscribing first if `Initialize` can be called multiple times) and unsubscribe in `OnDestroy()` (or an explicit `Cleanup()`/`Dispose()` method).

---

## 4. Audio & Haptics Standards
* All audio clips must be routed through `SoundManager` via `SoundType` enums; avoid bare `AudioSource.PlayClipAtPoint()`.
* Background music tracks (BGM) must be configured with seamless looping and dynamic crossfading.
* Haptic feedback (`Taptic`) must always check player preferences (`GameData.IsHapticEnabled`) before firing.

---

## 5. Editor Action Items & Handover Protocol (MANDATORY)

Whenever code, architecture, or asset modifications require manual intervention in the Unity Editor, the AI agent must **explicitly document and explain every required manual step in a clear, numbered, step-by-step checklist**.

* **Mandatory Reporting Triggers**:
  * **Inspector Assignments**: Any new or updated serialized fields (`[SerializeField]`) that need GameObject, Component, or Asset reference binding.
  * **Prefab & Component Updates**: Adding, modifying, or removing components on Scene GameObjects or Prefabs.
  * **Project & Build Settings**: Any changes requiring adjustments in Player Settings, Tags & Layers, Physics 2D/3D collision matrices, or Package Manager.
  * **Asset Import Configurations**: Audio load types (Streaming / Decompress On Load), texture compression, sprite slicing, or model settings.
  * **Scene Hierarchy & Wiring**: Placing new Manager prefabs into scenes, connecting UI Button `OnClick` events, or updating Canvas references.
* **Instruction Format Requirements**:
  * Clearly name the exact target asset, prefab, or scene GameObject hierarchy path.
  * Identify the exact Component and field name as displayed in the Inspector.
  * Specify the exact value, asset reference, or toggle state to apply.
  * Provide quick verification steps so the user can immediately test and confirm in Play Mode.

---

## 6. Review Checklist Before Committing
1. [ ] Is the code 100% in English (comments, variables, logs, tooltips)?
2. [ ] Are all private serialized fields encapsulated with `[SerializeField]`?
3. [ ] Are event subscriptions safely removed in `OnDestroy()` (or explicit cleanup) without unnecessary `Awake`/`OnEnable`/`OnDisable` hooks?
4. [ ] Are there any redundant allocations or un-cached component lookups in `Update()`?
5. [ ] Does the implementation conform to the Single Responsibility Principle?
6. [ ] Are all necessary Unity Editor manual actions listed step-by-step for the user?

