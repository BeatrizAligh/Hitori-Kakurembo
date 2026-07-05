# HitoriMultiplayer

Adaptacion multiplayer de Hitori Kakurembo basada en el modulo `Assets/TestMultiplayer`.

## Regla de arquitectura

No se modifica `Assets/TestMultiplayer`.

Este modulo hereda y especializa sus piezas principales:

- `HitoriMultiplayerSessionManager` hereda de `TestMultiplayerSessionManager`.
- `HitoriPlayerBrain` hereda de `TestMultiplayerPlayerBrain`.
- `HitoriPlayerPawn` hereda de `NetworkPawn`.
- `HitoriLocalInputDriver` hereda de `LocalBrainInputDriver`.
- Las UI wrapper heredan de las UI base para mantener el flujo probado mientras se prepara una UI final.

## Generar assets

En Unity ejecuta:

`Hitori Multiplayer > Build Multiplayer Assets`

Esto crea:

- `Assets/HitoriMultiplayer/Prefabs/HitoriPlayerBrain.prefab`
- `Assets/HitoriMultiplayer/Prefabs/HitoriSurvivorPawn.prefab`
- `Assets/HitoriMultiplayer/Prefabs/HitoriDollPawn.prefab`
- `Assets/HitoriMultiplayer/Prefabs/UI/HitoriMultiplayerButton.prefab`
- `Assets/HitoriMultiplayer/Scenes/HitoriMultiplayerMainMenu.unity`
- `Assets/HitoriMultiplayer/Scenes/HitoriMultiplayerLobby.unity`
- `Assets/HitoriMultiplayer/Scenes/HitoriMultiplayerGame.unity`

La escena de entrada del nuevo sistema es:

`Assets/HitoriMultiplayer/Scenes/HitoriMultiplayerMainMenu.unity`

## Sistema viejo

El bootstrap viejo `HitoriKakurembo.Core.ProjectBootstrap` queda desactivado por defecto.
Solo se reactiva si se agrega `HITORI_KAKUREMBO_LEGACY_BOOTSTRAP` a Scripting Define Symbols.
