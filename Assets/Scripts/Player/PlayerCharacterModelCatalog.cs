using UnityEngine;

namespace HitoriKakurembo.Player
{
    /// <summary>
    /// Describe un modelo visual disponible para representar al jugador.
    /// Este dato no controla gameplay; solo concentra nombre, ruta de arte y ajuste visual para que el sistema de red pueda sincronizar una seleccion por indice.
    /// </summary>
    public readonly struct PlayerCharacterModelDefinition
    {
        /// <summary>
        /// Indice estable usado por Netcode para sincronizar la seleccion entre host y clientes.
        /// </summary>
        public readonly int Index;

        /// <summary>
        /// Nombre legible mostrado en la interfaz de lobby.
        /// </summary>
        public readonly string DisplayName;

        /// <summary>
        /// Ruta del FBX dentro de Assets. En esta fase se usa para cargar el modelo en Play Mode dentro del editor.
        /// </summary>
        public readonly string AssetPath;

        /// <summary>
        /// Ruta opcional del Animator Controller asociado al modelo. Si esta vacia, el modelo queda sin controlador asignado.
        /// </summary>
        public readonly string AnimatorControllerPath;

        /// <summary>
        /// Nombre opcional del hijo renderizable dentro del FBX que debe quedar visible.
        /// Se usa cuando un mismo FBX contiene varios modelos superpuestos, como lucho_low/cultist_low o bear_low/bear_happy_low.
        /// </summary>
        public readonly string VisibleChildName;

        /// <summary>
        /// Posicion local base aplicada antes de normalizar el modelo contra el CharacterController.
        /// </summary>
        public readonly Vector3 LocalPosition;

        /// <summary>
        /// Rotacion local base aplicada al modelo instanciado.
        /// </summary>
        public readonly Vector3 LocalEulerAngles;

        /// <summary>
        /// Escala local base aplicada antes del autoajuste de altura.
        /// </summary>
        public readonly Vector3 LocalScale;

        /// <summary>
        /// Indica si este modelo debe usarse como visual de muneco cuando la ronda asigna ese estado.
        /// </summary>
        public readonly bool IsDollModel;

        /// <summary>
        /// Crea una definicion inmutable de modelo visual.
        /// </summary>
        /// <param name="index">Indice estable de seleccion.</param>
        /// <param name="displayName">Nombre mostrado en UI.</param>
        /// <param name="assetPath">Ruta del FBX en Assets.</param>
        /// <param name="animatorControllerPath">Ruta opcional del Animator Controller.</param>
        /// <param name="visibleChildName">Nombre del hijo renderizable que debe quedar visible dentro del FBX.</param>
        /// <param name="localPosition">Posicion local base.</param>
        /// <param name="localEulerAngles">Rotacion local base.</param>
        /// <param name="localScale">Escala local base.</param>
        /// <param name="isDollModel">Marca si es el modelo forzado para el muneco.</param>
        public PlayerCharacterModelDefinition(
            int index,
            string displayName,
            string assetPath,
            string animatorControllerPath,
            string visibleChildName,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale,
            bool isDollModel)
        {
            Index = index;
            DisplayName = displayName;
            AssetPath = assetPath;
            AnimatorControllerPath = animatorControllerPath;
            VisibleChildName = visibleChildName;
            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            LocalScale = localScale;
            IsDollModel = isDollModel;
        }
    }

    /// <summary>
    /// Catalogo central de modelos de personaje disponibles para el prototipo.
    /// Mantiene indices estables para que la seleccion pueda viajar por NetworkVariables sin depender de referencias directas a assets.
    /// </summary>
    public static class PlayerCharacterModelCatalog
    {
        /// <summary>
        /// Modelo humano inicial asignado a un jugador nuevo antes de que el usuario elija otro en el lobby.
        /// </summary>
        public const int DefaultModelIndex = 0;

        /// <summary>
        /// Modelo que se fuerza visualmente cuando el servidor marca a un jugador como muneco.
        /// </summary>
        public const int DollModelIndex = 4;

        /// <summary>
        /// Lista ordenada de modelos disponibles. El orden define como cicla el selector del lobby.
        /// </summary>
        private static readonly PlayerCharacterModelDefinition[] Models =
        {
            new PlayerCharacterModelDefinition(
                0,
                "Cultista Lucho",
                "Assets/Art/Characters/Cultist_Lucho/Cultist_Lucho.fbx",
                "Assets/Art/Characters/Cultist_Lucho/Cultist_Lucho.controller",
                "lucho_low",
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                false),
            new PlayerCharacterModelDefinition(
                1,
                "Cultista",
                "Assets/Art/Characters/Cultist_Lucho/Cultist_Lucho.fbx",
                "Assets/Art/Characters/Cultist_Lucho/Cultist_Lucho.controller",
                "cultist_low",
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                false),
            new PlayerCharacterModelDefinition(
                2,
                "Luisa",
                "Assets/Art/Characters/Luisa/Luisa.fbx",
                string.Empty,
                string.Empty,
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                false),
            new PlayerCharacterModelDefinition(
                3,
                "Osito",
                "Assets/Art/Characters/Bear/Osito.fbx",
                "Assets/Art/Characters/Bear/Osito.controller",
                "bear_low",
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                false),
            new PlayerCharacterModelDefinition(
                4,
                "Osito Feliz (Muneco)",
                "Assets/Art/Characters/Bear/Osito.fbx",
                "Assets/Art/Characters/Bear/Osito.controller",
                "bear_happy_low",
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                true)
        };

        /// <summary>
        /// Cantidad total de modelos disponibles para el selector.
        /// </summary>
        public static int Count => Models.Length;

        /// <summary>
        /// Devuelve una definicion valida para el indice solicitado, usando fallback seguro cuando el indice llega fuera de rango.
        /// </summary>
        /// <param name="index">Indice sincronizado o solicitado por UI.</param>
        /// <returns>Definicion de modelo lista para instanciarse.</returns>
        public static PlayerCharacterModelDefinition GetModel(int index)
        {
            int safeIndex = NormalizeIndex(index);
            return Models[safeIndex];
        }

        /// <summary>
        /// Devuelve el modelo configurado como muneco.
        /// </summary>
        /// <returns>Definicion visual del muneco.</returns>
        public static PlayerCharacterModelDefinition GetDollModel()
        {
            return GetModel(DollModelIndex);
        }

        /// <summary>
        /// Obtiene un nombre legible de modelo para mostrarlo en la UI de lobby o en logs.
        /// </summary>
        /// <param name="index">Indice de modelo.</param>
        /// <returns>Nombre visible del modelo.</returns>
        public static string GetDisplayName(int index)
        {
            return GetModel(index).DisplayName;
        }

        /// <summary>
        /// Normaliza un indice para que siempre apunte a un modelo existente.
        /// </summary>
        /// <param name="index">Indice recibido desde red, UI o fallback.</param>
        /// <returns>Indice valido dentro del catalogo.</returns>
        public static int NormalizeIndex(int index)
        {
            if (Models.Length == 0)
            {
                return 0;
            }

            return Mathf.Clamp(index, 0, Models.Length - 1);
        }

        /// <summary>
        /// Calcula el siguiente indice de manera circular para botones de anterior/siguiente.
        /// </summary>
        /// <param name="index">Indice base.</param>
        /// <param name="direction">Direccion del cambio; valores positivos avanzan y negativos retroceden.</param>
        /// <returns>Indice valido ciclado dentro del catalogo.</returns>
        public static int GetWrappedIndex(int index, int direction)
        {
            if (Models.Length == 0)
            {
                return 0;
            }

            int nextIndex = index + direction;

            while (nextIndex < 0)
            {
                nextIndex += Models.Length;
            }

            return nextIndex % Models.Length;
        }
    }
}
