# TestMultiplayer

Modulo independiente para probar un flujo multiplayer reutilizable con Netcode for GameObjects y Relay.

## Generar demo

En Unity ejecuta:

`Test Multiplayer > Build Demo Assets`

Esto crea:

- `Assets/TestMultiplayer/Prefabs/TestMultiplayerPlayerBrain.prefab`
- `Assets/TestMultiplayer/Prefabs/TestMultiplayerDemoPawn.prefab`
- `Assets/TestMultiplayer/Prefabs/UI/TestMultiplayerButton.prefab`
- `Assets/TestMultiplayer/Prefabs/UI/TestMultiplayerPlayerRow.prefab`
- `Assets/TestMultiplayer/Prefabs/UI/TestMultiplayerUIRoot.prefab`
- `Assets/TestMultiplayer/Prefabs/UI/Windows/TestMultiplayerMainWindow.prefab`
- `Assets/TestMultiplayer/Prefabs/UI/Windows/TestMultiplayerSessionWindow.prefab`
- `Assets/TestMultiplayer/Prefabs/UI/Windows/TestMultiplayerCustomizationWindow.prefab`
- `Assets/TestMultiplayer/Prefabs/UI/Windows/TestMultiplayerLobbyWindow.prefab`
- `Assets/TestMultiplayer/Prefabs/UI/Windows/TestMultiplayerConnectedPlayersHud.prefab`
- `Assets/TestMultiplayer/Scenes/TestMultiplayerMainMenu.unity`
- `Assets/TestMultiplayer/Scenes/TestMultiplayerLobby.unity`
- `Assets/TestMultiplayer/Scenes/TestMultiplayerGame.unity`

La escena principal es `TestMultiplayerMainMenu`.

## Arquitectura

- `TestMultiplayerSessionManager`: crea lobby Relay, se une por codigo, aprueba conexiones y carga escenas por NGO.
- `CharacterProfile`: nombre y datos de personalizacion enviados como JSON en el payload de conexion.
- `TestMultiplayerPlayerBrain`: cerebro de jugador. Sincroniza nombre, apariencia, estado listo y peon controlado.
- `NetworkPawn`: clase base para personajes jugables. Cada gameplay hereda de esta clase y decide como aplicar input.
- `DemoPawn`: peon de ejemplo que se mueve con WASD y colorea el material segun los datos de apariencia.
- `TestMultiplayerMainMenuUI`: controlador persistente de UI. Crea padres separados para Main, Crear/Unirse, Personalizacion, Lobby y HUD de jugadores.
- `TestMultiplayerUIRoot.prefab`: prefab raiz authorable con todas las ventanas de UI y referencias del flujo.
- `TestMultiplayerButton.prefab`: prefab unico usado por todos los botones del flujo para facilitar rebranding.
- `TestMultiplayerPlayerRow.prefab`: fila reutilizable del HUD de jugadores conectados.

El runtime no construye ventanas por codigo: instancia `TestMultiplayerUIRoot.prefab` y solo conecta eventos/actualiza datos. Puedes rebrandear las ventanas editando los prefabs de `Prefabs/UI/Windows`.

El modulo usa el namespace `TestMultiplayer` y no depende de scripts, escenas, prefabs ni servicios de Hitori Kakurembo.
