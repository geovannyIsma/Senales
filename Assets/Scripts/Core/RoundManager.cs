using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

public class RoundManager : MonoBehaviour
{
    [Header("Estado de Ronda")]
    [SerializeField] private int rondaActual = 0;
    public int RondaActual => rondaActual;

    [Header("Progreso de Ronda")]
    [SerializeField] private int senalesPendientes = 0;
    [SerializeField] private int senalesTotales = 0;
    [SerializeField] private int senalesCorrectas = 0;
    [SerializeField] private int senalesIncorrectas = 0;
    
    public int SenalesPendientes => senalesPendientes;
    public int SenalesTotales => senalesTotales;
    public int SenalesCorrectas => senalesCorrectas;
    public int SenalesIncorrectas => senalesIncorrectas;
    
    private List<TrafficSign> senalesEnEscena = new List<TrafficSign>();
    private SignalSpawner spawnerActivo;

    [Header("Eventos")]
    public UnityEvent<int, int> OnProgresoActualizado; // (identificadas, totales)
    public UnityEvent<TrafficSign, bool> OnSenalIdentificada; // (señal, fueCorrecta)

    [Header("Tracking de Errores")]
    public ErrorTracker errorTracker;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Buscar ErrorTracker si no está asignado
        if (errorTracker == null)
        {
            errorTracker = FindFirstObjectByType<ErrorTracker>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void IniciarRonda(ConfiguracionDificultad config)
    {
        // Obtener spawner de la zona actual
        spawnerActivo = GameManager.Instance.zoneManager.ObtenerSpawnerZonaActual();
        
        if (spawnerActivo == null)
        {
            Debug.LogError("No hay spawner en la zona actual");
            return;
        }

        // Obtener señales disponibles en esta zona
        var senalesDisponibles = GameManager.Instance.zoneManager.ObtenerSenalesDisponibles();
        
        if (senalesDisponibles.Count == 0)
        {
            Debug.LogError("No hay señales configuradas para esta zona");
            return;
        }

        // Preparar lista de señales variadas para la ronda
        List<TrafficSignData> senalesParaRonda = PrepararSenalesVariadas(
            config.cantidadSenales, 
            senalesDisponibles
        );
        
        // Generar señales en la escena (usamos null como objetivo ya que todas son válidas)
        spawnerActivo.GenerarSenalesParaRonda(null, senalesParaRonda);
        
        // Obtener referencias a las señales creadas
        senalesEnEscena = spawnerActivo.ObtenerSenalesActivas();
        
        // Inicializar contadores
        senalesTotales = senalesEnEscena.Count;
        senalesPendientes = senalesTotales;
        senalesCorrectas = 0;
        senalesIncorrectas = 0;
        
        OnProgresoActualizado?.Invoke(0, senalesTotales);

        Debug.Log($"Ronda {rondaActual + 1} iniciada. Identifica {senalesTotales} señales.");
    }

    /// <summary>
    /// Prepara una lista variada de señales para la ronda
    /// </summary>
    List<TrafficSignData> PrepararSenalesVariadas(int cantidadTotal, List<TrafficSignData> disponibles)
    {
        List<TrafficSignData> resultado = new List<TrafficSignData>();
        List<TrafficSignData> disponiblesCopia = new List<TrafficSignData>(disponibles);
        
        // Mezclar para variedad
        ShuffleList(disponiblesCopia);
        
        for (int i = 0; i < cantidadTotal; i++)
        {
            // Rotar entre las señales disponibles
            resultado.Add(disponiblesCopia[i % disponiblesCopia.Count]);
        }
        
        return resultado;
    }

    void ShuffleList<T>(List<T> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = lista[i];
            lista[i] = lista[j];
            lista[j] = temp;
        }
    }

    /// <summary>
    /// Llamado cuando el jugador intenta identificar una señal
    /// </summary>
    public bool IntentarIdentificarSenal(TrafficSign senal, string nombreIdentificado)
    {
        if (senal == null || senal.datos == null) return false;
        
        // Verificar si el nombre coincide con el tipo de señal
        bool esCorrecta = senal.datos.nombreSenal.Equals(nombreIdentificado, System.StringComparison.OrdinalIgnoreCase);
        
        if (esCorrecta)
        {
            senalesCorrectas++;
        }
        else
        {
            senalesIncorrectas++;
        }
        
        senalesPendientes--;
        
        int identificadas = senalesTotales - senalesPendientes;
        OnProgresoActualizado?.Invoke(identificadas, senalesTotales);
        OnSenalIdentificada?.Invoke(senal, esCorrecta);
        
        Debug.Log($"Señal {(esCorrecta ? "correcta" : "incorrecta")}: {senal.datos.nombreSenal}. Pendientes: {senalesPendientes}");
        
        // Verificar si se completó la ronda
        if (senalesPendientes <= 0)
        {
            GameManager.Instance?.RondaCompletada();
        }
        
        return esCorrecta;
    }

    /// <summary>
    /// Procesa la respuesta del menú de reconocimiento
    /// </summary>
    public void ProcesarRespuestaReconocimiento(TrafficSign senal, TrafficSignData respuestaElegida, bool esCorrecta)
    {
        if (senal == null) return;

        // CAMBIO: Usar tiempo desde interacción (tiempo de reconocimiento puro)
        // en lugar de tiempo desde creación (que incluye tiempo de búsqueda)
        float tiempoRespuesta = senal.ObtenerTiempoDesdeInteraccion();
        float tiempoBusqueda = senal.ObtenerTiempoBusqueda();
        
        Debug.Log($"[RoundManager] Tiempos - Respuesta: {tiempoRespuesta:F2}s, Búsqueda: {tiempoBusqueda:F2}s, Total: {senal.ObtenerTiempoDesdeCreacion():F2}s");

        // Marcar señal como identificada
        senal.MarcarComoIdentificada(esCorrecta);

        // NUEVO: Reproducir sonido según resultado
        if (esCorrecta)
        {
            GameAudioManager.Instance?.ReproducirCorrecto();
        }
        else
        {
            GameAudioManager.Instance?.ReproducirIncorrecto();
        }

        // Actualizar contadores
        if (esCorrecta)
        {
            senalesCorrectas++;
            errorTracker?.MarcarErrorCorregido(senal.datos?.nombreSenal);
        }
        else
        {
            senalesIncorrectas++;
            // CAMBIO: Pasar el tiempo de respuesta correcto
            errorTracker?.RegistrarError(senal, respuestaElegida, tiempoRespuesta);
        }
        
        senalesPendientes--;

        // Registrar métricas locales - CAMBIO: Usar tiempo de respuesta
        GameManager.Instance?.performanceTracker?.RegistrarIntento(
            senal.datos,
            esCorrecta,
            tiempoRespuesta, // CAMBIO: Era ObtenerTiempoDesdeCreacion()
            GameManager.Instance.difficultyManager.DificultadActual
        );

        // NUEVO: Enviar intento al servidor de métricas
        int zona = GameManager.Instance?.zoneManager?.ZonaActual ?? 0;
        int dificultad = (int)(GameManager.Instance?.difficultyManager?.DificultadActual ?? NivelDificultad.Baja);
        
        MetricsClient.Instance?.RegistrarIntento(
            senal.datos?.nombreSenal ?? "Desconocida",
            respuestaElegida?.nombreSenal,
            esCorrecta,
            tiempoRespuesta, // CAMBIO: Tiempo de reconocimiento puro
            zona,
            rondaActual,
            dificultad
        );

        // Notificar progreso
        int identificadas = senalesTotales - senalesPendientes;
        OnProgresoActualizado?.Invoke(identificadas, senalesTotales);
        OnSenalIdentificada?.Invoke(senal, esCorrecta);

        Debug.Log($"Señal {(esCorrecta ? "correcta" : "incorrecta")}: {senal.datos?.nombreSenal}. " +
                  $"Progreso: {identificadas}/{senalesTotales} (Correctas: {senalesCorrectas})");

        // Verificar si se completó la ronda
        if (senalesPendientes <= 0)
        {
            GameManager.Instance?.RondaCompletada();
        }
    }

    /// <summary>
    /// Versión legacy - mantener por compatibilidad
    /// </summary>
    public void RegistrarSenalSeleccionada(TrafficSign senal)
    {
        // Redirigir al nuevo sistema si no hay menú
        if (RecognitionMenuUI.Instance == null || !RecognitionMenuUI.Instance.EstaActivo())
        {
            ProcesarRespuestaReconocimiento(senal, senal.datos, true);
        }
    }

    /// <summary>
    /// Obtiene la lista de señales que aún no han sido seleccionadas
    /// </summary>
    public List<TrafficSign> ObtenerSenalesPendientes()
    {
        List<TrafficSign> pendientes = new List<TrafficSign>();
        foreach (var senal in senalesEnEscena)
        {
            if (senal != null && !senal.yaFueSeleccionada)
            {
                pendientes.Add(senal);
            }
        }
        return pendientes;
    }

    public void LimpiarRonda()
    {
        spawnerActivo?.LimpiarSenalesAnteriores();
        senalesEnEscena.Clear();
        senalesPendientes = 0;
        senalesTotales = 0;
        senalesCorrectas = 0;
        senalesIncorrectas = 0;
    }

    public void AvanzarRonda()
    {
        rondaActual++;
    }

    public void ReiniciarRondas()
    {
        rondaActual = 0;
    }

    public bool RondaCompleta()
    {
        return senalesPendientes <= 0;
    }
}
