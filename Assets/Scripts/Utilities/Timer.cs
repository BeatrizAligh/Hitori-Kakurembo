using System;
using UnityEngine;

namespace HitoriKakurembo.Utilities
{
    /// <summary>
    /// Representa un temporizador liviano que puede integrarse en sistemas que realizan avance manual del tiempo.
    /// </summary>
    [Serializable]
    public class Timer
    {
        /// <summary>
        /// Duracion por defecto, en segundos, utilizada cuando el temporizador inicia sin una duracion personalizada.
        /// </summary>
        [SerializeField] private float duration = 30f;

        /// <summary>
        /// Tiempo restante, en segundos, antes de que el temporizador termine.
        /// </summary>
        private float remainingTime;

        /// <summary>
        /// Obtiene un valor que indica si el temporizador se encuentra corriendo.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Obtiene un valor que indica si el temporizador termino y ya no esta corriendo.
        /// </summary>
        public bool IsFinished => !IsRunning && remainingTime <= 0f;

        /// <summary>
        /// Obtiene la duracion configurada del temporizador.
        /// </summary>
        public float Duration => duration;

        /// <summary>
        /// Obtiene el tiempo restante actual, garantizando un valor no negativo.
        /// </summary>
        public float RemainingTime => Mathf.Max(remainingTime, 0f);

        /// <summary>
        /// Inicia el temporizador utilizando la duracion configurada o una duracion personalizada.
        /// </summary>
        /// <param name="customDuration">
        /// Duracion personalizada en segundos. Un valor menor que cero mantiene la duracion serializada.
        /// </param>
        public void Start(float customDuration = -1f)
        {
            if (customDuration >= 0f)
            {
                duration = customDuration;
            }

            remainingTime = duration;
            IsRunning = true;
        }

        /// <summary>
        /// Avanza el temporizador restando el delta de tiempo indicado.
        /// </summary>
        /// <param name="deltaTime">
        /// Tiempo, en segundos, que debe descontarse del temporizador.
        /// </param>
        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            remainingTime -= deltaTime;

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                IsRunning = false;
            }
        }

        /// <summary>
        /// Detiene el temporizador sin modificar el tiempo restante.
        /// </summary>
        public void Stop()
        {
            IsRunning = false;
        }

        /// <summary>
        /// Restablece el tiempo restante a la duracion configurada y deja el temporizador detenido.
        /// </summary>
        public void Reset()
        {
            remainingTime = duration;
            IsRunning = false;
        }
    }
}
