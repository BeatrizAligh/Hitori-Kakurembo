using System;
using System.Collections.Generic;

namespace HitoriKakurembo.Core
{
    /// <summary>
    /// Proporciona un registro de servicios simple para compartir managers sin introducir referencias duras entre sistemas.
    /// </summary>
    public static class ServiceLocator
    {
        /// <summary>
        /// Almacena las instancias registradas indexadas por su tipo de servicio.
        /// </summary>
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        /// <summary>
        /// Registra o reemplaza la instancia asociada al tipo solicitado.
        /// </summary>
        /// <typeparam name="T">
        /// Tipo del servicio que se utilizara como clave de resolucion.
        /// </typeparam>
        /// <param name="service">
        /// Instancia del servicio que debe registrarse.
        /// </param>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                return;
            }

            Services[typeof(T)] = service;
        }

        /// <summary>
        /// Elimina el servicio asociado al tipo indicado.
        /// </summary>
        /// <typeparam name="T">
        /// Tipo del servicio que debe removerse del registro.
        /// </typeparam>
        public static void Unregister<T>() where T : class
        {
            Services.Remove(typeof(T));
        }

        /// <summary>
        /// Resuelve el servicio registrado para el tipo indicado.
        /// </summary>
        /// <typeparam name="T">
        /// Tipo del servicio solicitado.
        /// </typeparam>
        /// <returns>
        /// Instancia registrada para el tipo solicitado, o <see langword="null"/> cuando no existe registro.
        /// </returns>
        public static T Resolve<T>() where T : class
        {
            return TryResolve(out T service) ? service : null;
        }

        /// <summary>
        /// Intenta resolver el servicio registrado para el tipo indicado.
        /// </summary>
        /// <typeparam name="T">
        /// Tipo del servicio solicitado.
        /// </typeparam>
        /// <param name="service">
        /// Cuando el metodo retorna, contiene el servicio resuelto si existe; de lo contrario, <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> cuando el servicio se resolvio correctamente; en caso contrario, <see langword="false"/>.
        /// </returns>
        public static bool TryResolve<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object value))
            {
                service = value as T;
                return service != null;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Elimina todos los servicios actualmente registrados.
        /// </summary>
        public static void Clear()
        {
            Services.Clear();
        }
    }
}
