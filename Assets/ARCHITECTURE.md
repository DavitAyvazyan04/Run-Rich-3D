# Run Rich 3D architecture

The project follows the same responsibility split as the supplied Dram Inc
architecture reference and uses Extenject/Zenject for dependency injection.

## Runtime flow

`SceneContext` runs `RunRichSceneInstaller`, which binds the services and
creates `RunRichGameController` through the Zenject container.
The controller owns `RunRichGameState` and contains the game-loop decisions.
`RunRichGame` is the Unity view: it reads input, updates scene objects and renders
UI. Persistence is isolated behind `IPlayerProgressService`.

## Folders

- `Assets/Scripts/Common` - reusable services and interfaces.
- `Assets/Scripts/GameEngine/RunRich` - game-specific state and rules.
- `Assets/Scripts/Installers` - composition root (dependency wiring).
- `Assets/Scripts/RunRich` - existing Unity components and authored-scene view.
- `Assets/Scripts/Level Manager` - required company level lifecycle package.
- `Assets/Scripts/Level Manager/Editor` - its inspector plus level authoring tools.

The supplied `ButchersGames.LevelManager` remains the authoritative level-flow
component. Run Rich calls its `Init`, `StartLevel`, `RestartLevel` and
`NextLevel` API through `PlayerProgressService`; its `LevelsList` is assigned in
the authored scene.

## Dependency direction

`SceneContext -> MonoInstaller -> View -> Controller -> Service interface`

The controller does not depend on `MonoBehaviour`, UI, scene objects or
`PlayerPrefs`. Zenject resolves it through constructor injection and injects it
into `RunRichGame` using `[Inject]`.
