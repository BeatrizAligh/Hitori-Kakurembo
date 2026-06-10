namespace HitoriKakurembo.Seals
{
    /// <summary>
    /// Clasifica la forma conceptual de un sello ritual.
    /// El tipo permite que el spawn y la UI sepan si se trata de un talisman, dibujo, objeto fisico u otra variante futura.
    /// </summary>
    public enum SealKind
    {
        /// <summary>
        /// Talisman o papel ritual pegado sobre una pared.
        /// </summary>
        Talisman = 0,

        /// <summary>
        /// Amuleto o sello colgado desde techo, pared o soporte.
        /// </summary>
        HangingCharm = 1,

        /// <summary>
        /// Sello colocado sobre el piso.
        /// </summary>
        FloorSeal = 2,

        /// <summary>
        /// Dibujo ritual proyectado o dibujado sobre una superficie.
        /// </summary>
        RitualDrawing = 3,

        /// <summary>
        /// Marca sobrenatural sobre pared o suelo.
        /// </summary>
        WallMark = 4,

        /// <summary>
        /// Objeto ritual fisico que ocupa volumen en el mundo.
        /// </summary>
        PhysicalObject = 5
    }

    /// <summary>
    /// Define como debe colocarse un sello respecto a una superficie.
    /// Este dato guia la deteccion de superficie, la orientacion final y las validaciones espaciales.
    /// </summary>
    public enum SealPlacementType
    {
        /// <summary>
        /// El sello queda adherido a una pared.
        /// </summary>
        WallAttached = 0,

        /// <summary>
        /// El sello queda apoyado sobre el piso.
        /// </summary>
        FloorPlaced = 1,

        /// <summary>
        /// El sello queda colgado o anclado al techo.
        /// </summary>
        CeilingHanging = 2,

        /// <summary>
        /// El sello se dibuja o proyecta sobre una superficie valida.
        /// </summary>
        SurfaceDrawing = 3,

        /// <summary>
        /// El sello es un objeto de pie con soporte propio.
        /// </summary>
        FreeStanding = 4,

        /// <summary>
        /// Colocacion especial definida por sistemas futuros.
        /// </summary>
        Custom = 5
    }

    /// <summary>
    /// Estado autoritativo de un sello ritual durante la ronda.
    /// </summary>
    public enum SealState
    {
        /// <summary>
        /// El sello existe, pero aun no fue activado.
        /// </summary>
        Inactive = 0,

        /// <summary>
        /// Un superviviente esta completando la activacion.
        /// </summary>
        Activating = 1,

        /// <summary>
        /// El sello fue activado correctamente por los supervivientes.
        /// </summary>
        Active = 2,

        /// <summary>
        /// El muneco o entidad esta completando la desactivacion.
        /// </summary>
        Deactivating = 3,

        /// <summary>
        /// El sello fue corrompido por el muneco o entidad.
        /// </summary>
        Corrupted = 4,

        /// <summary>
        /// El sello queda fuera de uso y no participa mas hasta reiniciarse.
        /// </summary>
        Disabled = 5
    }
}
