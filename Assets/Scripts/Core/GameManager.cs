using UnityEngine;
using UnityEngine.Events;

public enum GameState
{
    Initializing,
    MainMenu,       // NUEVO: Pantalla de inicio
    ZoneIntro,      // Mostrando información de la zona
    RoundActive,    // Ronda en progreso
    RoundEvaluating,// Evaluando respuesta
    RoundEnd,       // Fin de ronda, preparando siguiente
    ZoneComplete,   // Zona completada
    GameComplete    // Juego terminado
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Referencias a Managers")]
    public ZoneManager zoneManager;
    public RoundManager roundManager;
    public TimerManager timerManager;
    public DifficultyManager difficultyManager;
    public PerformanceTracker performanceTracker;
    public ErrorTracker errorTracker;
    public FeedbackAIClient feedbackAIClient;
    public MetricsClient metricsClient;

    [Header("Configuración")]
    public int rondasPorZona = 6;                            // Máximo 6 rondas por zona
    public int rondasMinimasParaCompletar = 4;               // Mínimo 4 rondas (tiempo para subir dificultad)
    [Range(0f, 1f)] public float tasaAciertosMinima = 0.70f; // 70% para pasar (DEBE ser mayor que umbralSubir)
    public float tiempoPanelResultado = 5f;
    
    [Header("Configuración Menú Principal")]
    public bool mostrarMenuAlIniciar = true; // NUEVO: Controla si se muestra el menú
    public Transform puntoSpawnInicio; // NUEVO: Punto donde aparece el jugador al inicio
    public Transform xrOriginTransform; // NUEVO: Referencia al XR Origin para teletransporte

    [Header("Estado Actual")]
    [SerializeField] private GameState estadoActual = GameState.Initializing;
    public GameState EstadoActual => estadoActual;

    // Eventos
    public UnityEvent<GameState> OnEstadoCambiado;
    public UnityEvent<int> OnZonaCambiada;
    public UnityEvent<int> OnRondaIniciada;
    public UnityEvent<bool> OnRondaTerminada;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (errorTracker == null)
            errorTracker = FindFirstObjectByType<ErrorTracker>();
        if (feedbackAIClient == null)
            feedbackAIClient = FindFirstObjectByType<FeedbackAIClient>();
        if (metricsClient == null)
            metricsClient = FindFirstObjectByType<MetricsClient>();
        
        // NUEVO: Verificar que MetricsClient existe
        if (metricsClient == null)
        {
            Debug.LogWarning("GameManager: MetricsClient no encontrado. Creando uno automáticamente...");
            GameObject metricsObj = new GameObject("MetricsManager");
            metricsClient = metricsObj.AddComponent<MetricsClient>();
            DontDestroyOnLoad(metricsObj);
        }
        
        Invoke(nameof(InicializarJuego), 0.1f);
    }

    void InicializarJuego()
    {
        CambiarEstado(GameState.Initializing);
        
        difficultyManager.EstablecerDificultad(NivelDificultad.Baja);
        
        // NUEVO: Decidir si mostrar menú principal o ir directo al juego
        if (mostrarMenuAlIniciar)
        {
            MostrarMenuPrincipal();
        }
        else
        {
            IniciarZona(0);
        }
    }

    // NUEVO: Muestra la pantalla de inicio
    void MostrarMenuPrincipal()
    {
        CambiarEstado(GameState.MainMenu);
        
        // El MainMenuUI debería reaccionar al cambio de estado
        if (MainMenuUI.Instance != null)
        {
            MainMenuUI.Instance.MostrarMenu();
        }
        
        Debug.Log("GameManager: Mostrando menú principal");
    }

    // NUEVO: Llamado por MainMenuUI cuando el jugador presiona "Iniciar"
    public void IniciarDesdeMenuPrincipal()
    {
        if (estadoActual != GameState.MainMenu)
        {
            Debug.LogWarning("IniciarDesdeMenuPrincipal llamado fuera del estado MainMenu");
            return;
        }
        
        Debug.Log("GameManager: Iniciando juego desde menú principal");
        
        // NUEVO: Reiniciar TODO para nuevo juego (incluye métricas globales)
        performanceTracker?.ReiniciarJuegoCompleto();
        errorTracker?.ReiniciarSesion();
        
        // CAMBIO: Iniciar sesión de métricas y esperar confirmación
        if (metricsClient != null)
        {
            metricsClient.OnSesionCreada += OnSesionCreadaCallback;
            metricsClient.IniciarSesion((int)difficultyManager.DificultadActual);
        }
        else
        {
            // Si no hay metricsClient, continuar sin métricas
            IniciarZona(0);
        }
    }

    // NUEVO: Callback cuando la sesión se crea exitosamente
    void OnSesionCreadaCallback(int sesionId)
    {
        Debug.Log($"GameManager: Sesión de métricas creada con ID: {sesionId}");
        
        // Desuscribirse para evitar múltiples llamadas
        if (metricsClient != null)
        {
            metricsClient.OnSesionCreada -= OnSesionCreadaCallback;
        }
        
        // Ahora sí iniciar la zona
        IniciarZona(0);
    }

    public void IniciarZona(int indiceZona)
    {
        zoneManager.ActivarZona(indiceZona);
        OnZonaCambiada?.Invoke(indiceZona);
        CambiarEstado(GameState.ZoneIntro);
    }

    public void OnBotonIniciarPresionado()
    {
        if (estadoActual == GameState.ZoneIntro)
        {
            IniciarNuevaRonda();
        }
    }

    // Reemplazar la lógica en IniciarNuevaRonda
    public void IniciarNuevaRonda()
    {
        if (EvaluarCompletitudZona())
        {
            FinalizarZona();
            return;
        }

        CambiarEstado(GameState.RoundActive);
        
        var config = difficultyManager.ObtenerConfiguracion();
        
        Debug.Log($"[GameManager] ========================================");
        Debug.Log($"[GameManager] INICIANDO RONDA {roundManager.RondaActual + 1}");
        Debug.Log($"[GameManager] Dificultad: {difficultyManager.DificultadActual}");
        Debug.Log($"[GameManager] Señales a generar: {config.cantidadSenales}");
        Debug.Log($"[GameManager] Tiempo por señal: {config.tiempoSegundos}s");
        Debug.Log($"[GameManager] ========================================");
        
        roundManager.IniciarRonda(config);
        
        timerManager.ConfigurarTemporizador(config.tiempoSegundos); 
        timerManager.DetenerTemporizador();
        
        OnRondaIniciada?.Invoke(roundManager.RondaActual);
    }

    // NUEVO: Se llama al interactuar con una señal específica
    public void IniciarDesafioSenal()
    {
        if (estadoActual != GameState.RoundActive) return;

        var config = difficultyManager.ObtenerConfiguracion();
        // Inicia la cuenta regresiva (ej. 5 segundos)
        timerManager.IniciarTemporizador(config.tiempoSegundos);
        Debug.Log("Desafío de señal iniciado - Tiempo corriendo");
    }

    // NUEVO: Se llama al responder (antes de procesar validez)
    public void DetenerDesafioSenal()
    {
        timerManager.DetenerTemporizador();
        
        // FIX: Resetear visualmente el temporizador (ponerlo en 0 o vaciarlo)
        // para que no se quede con el número congelado "2.4"
        timerManager.ConfigurarTemporizador(0); 
    }

    /// <summary>
    /// Llamado cuando el jugador identifica TODAS las señales de la ronda
    /// </summary>
    public void RondaCompletada()
    {
        if (estadoActual != GameState.RoundActive) return;
        
        CambiarEstado(GameState.RoundEvaluating);
        timerManager.DetenerTemporizador();
        
        // NUEVO: Reproducir sonido de ronda completada
        GameAudioManager.Instance?.ReproducirRondaCompletada();
        
        Debug.Log($"¡Ronda completada! {roundManager.SenalesCorrectas}/{roundManager.SenalesTotales} señales identificadas");
        
        OnRondaTerminada?.Invoke(true);
        
        // CAMBIO: Usar variable configurable en lugar de valor fijo
        Invoke(nameof(PrepararSiguienteRonda), tiempoPanelResultado);
    }

    /// <summary>
    /// Método legacy - mantener por compatibilidad pero ya no controla el flujo principal
    /// </summary>
    public void RegistrarRespuesta(TrafficSignData senalSeleccionada, float tiempoRespuesta)
    {
    }

    public void TiempoAgotado()
    {
        if (estadoActual != GameState.RoundActive) return;
        
        Debug.Log("Tiempo agotado para la señal actual.");

        if (RecognitionMenuUI.Instance != null && RecognitionMenuUI.Instance.EstaActivo())
        {
            var senalActual = RecognitionMenuUI.Instance.SenalSeleccionada;
            
            RecognitionMenuUI.Instance.OcultarMenu();

            if (senalActual != null)
            {
                // NUEVO: Registrar error por tiempo agotado
                errorTracker?.RegistrarErrorTiempoAgotado(senalActual);
                
                roundManager.ProcesarRespuestaReconocimiento(senalActual, null, false);
            }
        }
        else
        {
            timerManager.DetenerTemporizador();
        }
    }

    void PrepararSiguienteRonda()
    {
        CambiarEstado(GameState.RoundEnd);
        
        // Limpiar señales anteriores
        roundManager.LimpiarRonda();
        
        // Siguiente ronda
        roundManager.AvanzarRonda();
        
        // CAMBIO: Ajustar dificultad Y ESPERAR respuesta del ML antes de iniciar
        var metricas = performanceTracker.ObtenerMetricasRecientes();
        Debug.Log($"[GameManager] Preparando siguiente ronda. Solicitando ajuste de dificultad...");
        
        difficultyManager.EvaluarYAjustar(metricas, () => {
            // Este callback se ejecuta DESPUÉS de recibir la respuesta del ML
            Debug.Log($"[GameManager] Dificultad actualizada. Iniciando ronda con {difficultyManager.ObtenerConfiguracion().cantidadSenales} señales");
            IniciarNuevaRonda();
        });
    }

    void FinalizarZona()
    {
        CambiarEstado(GameState.ZoneComplete);
        
        // NUEVO: Reproducir sonido de zona completada
        GameAudioManager.Instance?.ReproducirZonaCompletada();
        
        int siguienteZona = zoneManager.ZonaActual + 1;
        
        if (siguienteZona < zoneManager.TotalZonas)
        {
            // CAMBIO: Usar tiempo configurable también para transición de zona
            Invoke(nameof(IrASiguienteZona), tiempoPanelResultado);
        }
        else
        {
            CambiarEstado(GameState.GameComplete);
            Debug.Log("¡Juego Completado!");
        }
    }

    /// <summary>
    /// Evalúa si la zona actual cumple criterios de completitud
    /// </summary>
    bool EvaluarCompletitudZona()
    {
        int rondaActual = roundManager.RondaActual;
        
        // Mínimo de rondas requerido
        if (rondaActual < rondasMinimasParaCompletar)
            return false;
        
        // Evaluar tasa de aciertos
        var metricas = performanceTracker.ObtenerMetricasRecientes();
        
        if (metricas.tasaAciertos >= tasaAciertosMinima)
        {
            Debug.Log($"Zona completada: {metricas.tasaAciertos:P0} aciertos en {rondaActual} rondas");
            return true;
        }
        
        // Si supera máximo de rondas, completar de todos modos
        if (rondaActual >= rondasPorZona)
        {
            Debug.Log($"Zona completada por límite de rondas ({rondasPorZona})");
            return true;
        }
        
        return false;
    }

    void IrASiguienteZona()
    {
        roundManager.ReiniciarRondas();
        
        // CAMBIO: Reiniciar TODAS las métricas para la nueva zona (no solo las recientes)
        // Así la precisión empieza en 100% en cada zona nueva
        performanceTracker?.ReiniciarMetricas();
        
        int siguienteZona = zoneManager.ZonaActual + 1;
        difficultyManager.AjustarDificultadParaZona(siguienteZona, zoneManager.zonas[siguienteZona]);
        
        IniciarZona(siguienteZona);
    }

    void CambiarEstado(GameState nuevoEstado)
    {
        estadoActual = nuevoEstado;
        OnEstadoCambiado?.Invoke(nuevoEstado);
        Debug.Log($"Estado del juego: {nuevoEstado}");
        
        // CAMBIO: Usar métricas del juego completo al finalizar
        if (nuevoEstado == GameState.GameComplete)
        {
            var metricas = performanceTracker.ObtenerMetricasJuegoCompleto(); // CAMBIO: Era ObtenerMetricasGlobales()
            metricsClient?.FinalizarSesion(
                metricas,
                zoneManager.ZonaActual + 1,
                zoneManager.ZonaActual,
                true
            );
        }
    }

    // NUEVO: Método para volver al menú principal
    public void VolverAlMenuPrincipal()
    {
        // CAMBIO: Usar métricas del juego completo
        if (metricsClient?.SesionActualId >= 0)
        {
            var metricas = performanceTracker.ObtenerMetricasJuegoCompleto(); // CAMBIO
            metricsClient?.FinalizarSesion(
                metricas,
                zoneManager?.ZonaActual ?? 0,
                zoneManager?.ZonaActual ?? 0,
                false // No completada
            );
        }
        
        // Limpiar estado actual
        roundManager?.LimpiarRonda();
        roundManager?.ReiniciarRondas();
        // NO llamar ReiniciarMetricas aquí - se hará al iniciar nuevo juego
        errorTracker?.ReiniciarSesion();
        
        if (zoneManager?.ZonaActual >= 0)
        {
            zoneManager.zonas[zoneManager.ZonaActual].controlador?.DesactivarZona();
        }
        
        // NUEVO: Teletransportar al jugador al punto de inicio
        TeletransportarAlInicio();
        
        MostrarMenuPrincipal();
    }
    
    // NUEVO: Teletransporta al jugador al punto de spawn inicial
    void TeletransportarAlInicio()
    {
        Transform xrOrigin = ObtenerXROrigin();
        
        if (xrOrigin == null)
        {
            Debug.LogWarning("GameManager: No se pudo encontrar XR Origin para teletransportar");
            return;
        }
        
        if (puntoSpawnInicio != null)
        {
            // Calcular offset de cámara para posicionamiento preciso
            Camera camaraVR = Camera.main;
            if (camaraVR != null)
            {
                Vector3 offsetCamara = xrOrigin.position - camaraVR.transform.position;
                offsetCamara.y = 0; // Solo offset horizontal
                
                Vector3 posicionFinal = puntoSpawnInicio.position + offsetCamara;
                posicionFinal.y = puntoSpawnInicio.position.y;
                
                xrOrigin.position = posicionFinal;
            }
            else
            {
                xrOrigin.position = puntoSpawnInicio.position;
            }
            
            // Aplicar rotación (solo Y para VR)
            Vector3 rotacionActual = xrOrigin.eulerAngles;
            xrOrigin.rotation = Quaternion.Euler(rotacionActual.x, puntoSpawnInicio.eulerAngles.y, rotacionActual.z);
            
            Debug.Log($"GameManager: Jugador teletransportado al punto de inicio: {puntoSpawnInicio.position}");
        }
        else
        {
            Debug.LogWarning("GameManager: puntoSpawnInicio no asignado. El jugador permanecerá en su posición actual.");
        }
    }
    
    // NUEVO: Obtiene el XR Origin para teletransporte
    Transform ObtenerXROrigin()
    {
        // 1. Usar referencia manual si está asignada
        if (xrOriginTransform != null)
            return xrOriginTransform;
        
        // 2. Buscar por tag XR Origin
        GameObject xrOriginObj = GameObject.FindWithTag("XR Origin");
        if (xrOriginObj != null)
            return xrOriginObj.transform;
        
        // 3. Buscar por nombre común
        var xrOriginByName = GameObject.Find("XR Origin (XR Rig)");
        if (xrOriginByName != null)
            return xrOriginByName.transform;
        
        xrOriginByName = GameObject.Find("XR Origin");
        if (xrOriginByName != null)
            return xrOriginByName.transform;
        
        // 4. Buscar componente XROrigin
        var xrOriginComponent = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOriginComponent != null)
            return xrOriginComponent.transform;
        
        return null;
    }
}
