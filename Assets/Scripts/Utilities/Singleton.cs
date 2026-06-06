using UnityEngine;

namespace HitoriKakurembo.Utilities
{
    /// <summary>
    /// Proporciona una implementacion base de singleton para managers que deben exponer una unica instancia global.
    /// </summary>
    /// <typeparam name="T">
    /// Tipo concreto del manager que hereda de <see cref="MonoBehaviour"/>.
    /// </typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// Obtiene la instancia singleton activa del tipo solicitado.
        /// </summary>
        public static T Instance { get; private set; }

        /// <summary>
        /// Inicializa la instancia singleton y elimina duplicados cuando existen varios objetos del mismo tipo.
        /// </summary>
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this as T;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Limpia la referencia estatica cuando la instancia singleton activa se destruye.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
