using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Paneles")]
    public GameObject panelMenuPrincipal; // NUEVO: Panel de inicio
    public GameObject panelIntroZona;
    public GameObject panelJuego; // ESTE ES EL HUD (Canvas_HUD)
    public GameObject panelResultado;
    public GameObject panelFinJuego;

    [Header("Posicionamiento VR General")]
    public Transform referenciaJugador;
    public float distanciaUI = 3f;
    public float alturaUI = 0f; // CAMBIO: De 0.5f a 0f para que quede a nivel de ojos
    public Canvas canvasPrincipal; 

    [Header("Configuración HUD (Panel Juego)")]
    public bool usarHUDEnMano = false; // CAMBIO: Desactivar modo mano por defecto
    public bool usarHUDCasco = true;   // NUEVO: Modo casco (head-locked)
    public Transform anchorMano; // Para modo muñeca
    public Transform anchorCabeza; // NUEVO: Arrastra aquí la Main Camera o XR Camera
    
    [Header("Configuración Modo Mano")]
    public Vector3 offsetMano = new Vector3(0, 0.05f, 0);
    public Vector3 rotacionMano = new Vector3(90, 0, 0);
    public float escalaHUDMano = 0.0005f;

    [Header("Configuración Modo Casco (Head-Locked)")]
    public Vector3 offsetCasco = new Vector3(0, -0.15f, 0.5f); // Abajo y adelante de la vista
    public Vector3 rotacionCasco = Vector3.zero; // Mirando hacia el jugador
    public float escalaHUDCasco = 0.0008f; // Un poco más grande que en mano

    [Header("Texto de Información")]
    public TextMeshProUGUI textoNombreZona;
    public TextMeshProUGUI textoDescripcionZona;
    public TextMeshProUGUI textoInstruccion; // "Busca la señal: PARE"
    public TextMeshProUGUI textoTemporizador;
    public TextMeshProUGUI textoDificultad;
    public TextMeshProUGUI textoRonda;

    [Header("Feedback")]
    public TextMeshProUGUI textoResultado;
    public Image imagenResultado;
    public Sprite spriteCorrecta;
    public Sprite spriteIncorrecta;
    public TextMeshProUGUI textoMetricasRonda; // NUEVO: Arrastra aquí el campo metricas_ronda

    [Header("Métricas (Opcional)")]
    public TextMeshProUGUI textoAciertos;
    public TextMeshProUGUI textoTasaAciertos;
    public TextMeshProUGUI textoTiempoPromedio;
    public TextMeshProUGUI textoMensajeIA; // NUEVO: Campo separado para mensajes de IA

    [Header("Progreso de Ronda")]
    public TextMeshProUGUI textoProgreso; // "Identificadas: 2/5"

    [Header("Botones de Navegación")]
    public Button botonIniciarZona; // Arrastra aquí el Boton_Iniciar del Panel_Intro_Zona
    public Button botonVolverMenu; // NUEVO: Botón para volver al menú principal
    public Button botonVolverAlInicioFinJuego; // NUEVO: Botón en Panel_Fin_Juego para regresar al inicio

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // --- PLACEHOLDERS INICIALES ---
        if (textoTemporizador != null) textoTemporizador.text = "00:00";
        if (textoDificultad != null) textoDificultad.text = "Dificultad: Baja";
        if (textoAciertos != null) textoAciertos.text = "Aciertos: 0 | Errores: 0";
        if (textoTasaAciertos != null) textoTasaAciertos.text = "Precisión: 100%";
        if (textoTiempoPromedio != null) textoTiempoPromedio.text = "Tiempo: --";
        if (textoMensajeIA != null) textoMensajeIA.text = "";
        if (textoProgreso != null) textoProgreso.text = "Esperando inicio...";

        if (referenciaJugador == null)
        {
            referenciaJugador = Camera.main?.transform;
        }

        if (anchorCabeza == null)
        {
            anchorCabeza = Camera.main?.transform;
        }

        if (canvasPrincipal == null)
        {
            canvasPrincipal = GetComponentInParent<Canvas>();
        }

        // Suscribirse a eventos del GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnEstadoCambiado.AddListener(OnCambioEstado);
            GameManager.Instance.OnZonaCambiada.AddListener(OnCambioZona);
            GameManager.Instance.OnRondaIniciada.AddListener(OnInicioRonda);
            GameManager.Instance.OnRondaTerminada.AddListener(OnFinRonda);
        }

        if (GameManager.Instance?.timerManager != null)
        {
            GameManager.Instance.timerManager.OnTiempoActualizado.AddListener(ActualizarTemporizador);
            GameManager.Instance.timerManager.OnAdvertenciaTiempo.AddListener(MostrarAdvertenciaTiempo);
        }

        if (GameManager.Instance?.difficultyManager != null)
        {
            GameManager.Instance.difficultyManager.OnDificultadCambiada.AddListener(OnCambioDificultad);
            GameManager.Instance.difficultyManager.OnMensajeModeloIA.AddListener(MostrarMensajeModeloIA); 
        }

        if (GameManager.Instance?.roundManager != null)
        {
            GameManager.Instance.roundManager.OnProgresoActualizado.AddListener(ActualizarProgreso);
            GameManager.Instance.roundManager.OnSenalIdentificada.AddListener(OnSenalIdentificada); // NUEVO
        }

        // NUEVO: Suscribirse a actualizaciones de métricas en tiempo real
        if (GameManager.Instance?.performanceTracker != null)
        {
            GameManager.Instance.performanceTracker.OnMetricasActualizadas.AddListener(OnMetricasActualizadasTiempoReal);
        }

        // Configurar botón de iniciar zona
        if (botonIniciarZona != null)
        {
            botonIniciarZona.onClick.AddListener(OnBotonIniciarClick);
        }
        
        if (botonVolverMenu != null)
        {
            botonVolverMenu.onClick.AddListener(OnBotonVolverMenuClick);
        }
        
        // NUEVO: Configurar botón de volver al inicio en Panel_Fin_Juego
        if (botonVolverAlInicioFinJuego != null)
        {
            botonVolverAlInicioFinJuego.onClick.AddListener(OnBotonVolverMenuClick);
        }

        // --- SINCRONIZACIÓN INICIAL ---
        if (GameManager.Instance != null)
        {
            Debug.Log($"UIManager Start: Sincronizando estado inicial: {GameManager.Instance.EstadoActual}");
            OnCambioEstado(GameManager.Instance.EstadoActual);
            
            int zonaActual = GameManager.Instance.zoneManager?.ZonaActual ?? -1;
            if (zonaActual >= 0)
            {
                Debug.Log($"UIManager Start: Sincronizando zona inicial: {zonaActual}");
                OnCambioZona(zonaActual);
            }

            // NUEVO: Sincronizar dificultad inicial
            var dificultadActual = GameManager.Instance.difficultyManager?.DificultadActual ?? NivelDificultad.Baja;
            ultimaDificultad = dificultadActual;
            ActualizarVisualDificultad(dificultadActual);
            
            // NUEVO: Sincronizar métricas iniciales
            ActualizarMetricasTiempoReal();
        }
        else
        {
            OcultarTodosPaneles();
        }
    }

    void LateUpdate()
    {
        // 1. Lógica para paneles GRANDES (Intro, Fin, Resultado)
        if (PanelGrandeActivo())
        {
            PosicionarUIFrenteAlJugador();
        }

        // 2. Lógica para el HUD (Durante el juego)
        if (panelJuego != null && panelJuego.activeSelf)
        {
            if (usarHUDCasco && anchorCabeza != null)
            {
                PosicionarHUDCasco();
            }
            else if (usarHUDEnMano && anchorMano != null)
            {
                PosicionarHUDEnMano();
            }
            else
            {
                // Fallback: flotar frente a cámara
                if (panelJuego.transform.parent != null)
                {
                    panelJuego.transform.SetParent(null);
                }
                PosicionarElementoFrenteCamara(panelJuego.transform);
            }
        }
    }

    bool PanelGrandeActivo()
    {
        return (panelMenuPrincipal != null && panelMenuPrincipal.activeSelf) || // NUEVO
               (panelIntroZona != null && panelIntroZona.activeSelf) ||
               (panelResultado != null && panelResultado.activeSelf) ||
               (panelFinJuego != null && panelFinJuego.activeSelf);
    }

    void PosicionarHUDEnMano()
    {
        // El HUD sigue la mano/muñeca
        if (panelJuego == null || anchorMano == null) return;

        // --- SOLUCIÓN PARPADEO: EMPARENTAR ---
        // Al hacer que el panel sea hijo real del controlador, Unity gestiona el movimiento
        // de forma interna, eliminando el "jitering" o parpadeo al mover la cámara.
        if (panelJuego.transform.parent != anchorMano)
        {
            panelJuego.transform.SetParent(anchorMano);
            // Resetear transformaciones locales para evitar que salga disparado
            panelJuego.transform.localPosition = Vector3.zero;
            panelJuego.transform.localRotation = Quaternion.identity;
        }

        // Ahora aplicamos los offsets de forma LOCAL (relativo a la mano)
        // Esto fija el HUD a la muñeca como si fuera un reloj pegado
        panelJuego.transform.localPosition = offsetMano;
        panelJuego.transform.localRotation = Quaternion.Euler(rotacionMano);
        
        // Forzar escala
        panelJuego.transform.localScale = Vector3.one * escalaHUDMano;
    }

    /// <summary>
    /// Posiciona el HUD fijo a la cabeza del jugador (estilo casco/visor)
    /// </summary>
    void PosicionarHUDCasco()
    {
        if (panelJuego == null || anchorCabeza == null) return;

        // Emparentar a la cabeza para seguimiento perfecto sin jittering
        if (panelJuego.transform.parent != anchorCabeza)
        {
            panelJuego.transform.SetParent(anchorCabeza);
            // Resetear transformaciones locales
            panelJuego.transform.localPosition = Vector3.zero;
            panelJuego.transform.localRotation = Quaternion.identity;
        }

        // Aplicar offset local (relativo a la cámara/cabeza)
        panelJuego.transform.localPosition = offsetCasco;
        panelJuego.transform.localRotation = Quaternion.Euler(rotacionCasco);
        
        // Escala del HUD
        panelJuego.transform.localScale = Vector3.one * escalaHUDCasco;
    }

    void PosicionarUIFrenteAlJugador()
    {
        if (referenciaJugador == null || canvasPrincipal == null) return;
        PosicionarElementoFrenteCamara(canvasPrincipal.transform);
    }
    
    // Función auxiliar para ambos casos
    void PosicionarElementoFrenteCamara(Transform elemento)
    {
        Vector3 direccion = referenciaJugador.forward;
        direccion.y = 0;
        if (direccion.sqrMagnitude < 0.01f) direccion = Vector3.forward;
        direccion.Normalize();

        Vector3 nuevaPosicion = referenciaJugador.position + direccion * distanciaUI;
        
        // CAMBIO: Usar la altura de la cámara directamente + offset
        nuevaPosicion.y = referenciaJugador.position.y + alturaUI;

        elemento.position = Vector3.Lerp(elemento.position, nuevaPosicion, Time.deltaTime * 5f);

        Vector3 lookDir = referenciaJugador.position - elemento.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-lookDir);
            elemento.rotation = Quaternion.Slerp(elemento.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    void OcultarTodosPaneles()
    {
        panelMenuPrincipal?.SetActive(false); // NUEVO
        panelIntroZona?.SetActive(false);
        panelJuego?.SetActive(false);
        panelResultado?.SetActive(false);
        panelFinJuego?.SetActive(false);
    }

    void OnCambioEstado(GameState nuevoEstado)
    {
        OcultarTodosPaneles();
        Debug.Log($"UIManager: Cambio de estado UI a {nuevoEstado}");

        switch (nuevoEstado)
        {
            // NUEVO: Manejar estado MainMenu
            case GameState.MainMenu:
                if (panelMenuPrincipal != null)
                {
                    panelMenuPrincipal.SetActive(true);
                    panelMenuPrincipal.transform.SetAsLastSibling();
                }
                if (canvasPrincipal != null) canvasPrincipal.transform.localScale = Vector3.one * 0.001f;
                break;
                
            case GameState.ZoneIntro:
                if (panelIntroZona != null)
                {
                    panelIntroZona.SetActive(true);
                    panelIntroZona.transform.SetAsLastSibling();
                }
                if (canvasPrincipal != null) canvasPrincipal.transform.localScale = Vector3.one * 0.001f; 
                break;
            case GameState.RoundActive:
                if (panelJuego != null) 
                {
                    panelJuego.SetActive(true);
                    Debug.Log("UIManager: Activando HUD de Juego (RoundActive)");
                }
                break;
            case GameState.RoundEvaluating:
                if (panelResultado != null)
                {
                    panelResultado.SetActive(true);
                    panelResultado.transform.SetAsLastSibling();
                }
                if (canvasPrincipal != null) canvasPrincipal.transform.localScale = Vector3.one * 0.001f;
                break;
            case GameState.GameComplete:
                if (panelFinJuego != null)
                {
                    panelFinJuego.SetActive(true);
                    panelFinJuego.transform.SetAsLastSibling();
                }
                MostrarEstadisticasFinales();
                if (canvasPrincipal != null) canvasPrincipal.transform.localScale = Vector3.one * 0.001f;
                break;
        }
    }

    void OnCambioZona(int indiceZona)
    {
        var zonaData = GameManager.Instance?.zoneManager?.ZonaActualData;
        if (zonaData != null)
        {
            if (textoNombreZona != null) textoNombreZona.text = zonaData.nombreZona;
            if (textoDescripcionZona != null) textoDescripcionZona.text = zonaData.descripcion;
        }
        
        // NUEVO: Forzar actualización de métricas al cambiar de zona
        // Esto asegura que el HUD muestre 100% al inicio de cada zona
        ActualizarMetricasTiempoReal();
        
        Debug.Log($"UIManager: Zona cambiada a {indiceZona}, métricas actualizadas");
    }

    void OnInicioRonda(int numeroRonda)
    {
        if (textoRonda != null)
            textoRonda.text = $"Ronda {numeroRonda + 1}";

        int totalSenales = GameManager.Instance?.roundManager?.SenalesTotales ?? 0;
        if (textoInstruccion != null)
        {
            textoInstruccion.text = $"Identifica todas las señales\n<b>({totalSenales} señales)</b>";
        }
        
        if (textoTemporizador != null)
        {
            textoTemporizador.color = Color.white;
        }

        // NUEVO: Actualizar dificultad al iniciar ronda
        var dificultadActual = GameManager.Instance?.difficultyManager?.DificultadActual ?? NivelDificultad.Baja;
        ultimaDificultad = dificultadActual;
        ActualizarVisualDificultad(dificultadActual);

        // NUEVO: Actualizar métricas al iniciar ronda
        ActualizarMetricasTiempoReal();
        
        Debug.Log($"UIManager: Ronda {numeroRonda + 1} iniciada, Dificultad: {dificultadActual}");
    }

    // NUEVO: Llamado cuando se identifica una señal
    void OnSenalIdentificada(TrafficSign senal, bool fueCorrecta)
    {
        // Actualizar métricas inmediatamente
        ActualizarMetricasTiempoReal();
    }

    // NUEVO: Llamado por el evento OnMetricasActualizadas del PerformanceTracker
    void OnMetricasActualizadasTiempoReal(MetricasRecientes metricas)
    {
        ActualizarMetricasTiempoReal();
    }

    // NUEVO: Método centralizado para actualizar métricas del HUD
    void ActualizarMetricasTiempoReal()
    {
        var tracker = GameManager.Instance?.performanceTracker;
        if (tracker == null) return;

        var metricas = tracker.ObtenerMetricasGlobales();
        
        // Actualizar aciertos/errores
        if (textoAciertos != null)
        {
            textoAciertos.text = $"Aciertos: {metricas.aciertos} | Errores: {metricas.errores}";
        }
        
        // Actualizar precisión usando el método centralizado
        if (textoTasaAciertos != null)
        {
            float precision = tracker.CalcularPrecisionGlobal();
            textoTasaAciertos.text = $"Precisión: {precision:F0}%";
            
            // Color según rendimiento
            if (precision >= 80f)
                textoTasaAciertos.color = Color.green;
            else if (precision >= 50f)
                textoTasaAciertos.color = Color.yellow;
            else
                textoTasaAciertos.color = Color.red;
        }

        // Actualizar tiempo promedio
        if (textoTiempoPromedio != null)
        {
            if (metricas.tiempoPromedioRespuesta > 0)
                textoTiempoPromedio.text = $"Tiempo: {metricas.tiempoPromedioRespuesta:F1}s";
            else
                textoTiempoPromedio.text = "Tiempo: --";
        }
    }

    void OnFinRonda(bool fueCorrecta)
    {
        int correctas = GameManager.Instance?.roundManager?.SenalesCorrectas ?? 0;
        int incorrectas = GameManager.Instance?.roundManager?.SenalesIncorrectas ?? 0;
        int totales = GameManager.Instance?.roundManager?.SenalesTotales ?? 0;
        
        if (textoResultado != null)
        {
            if (correctas == totales)
            {
                textoResultado.text = $"¡Perfecto!\n{correctas}/{totales} correctas";
                textoResultado.color = Color.green;
            }
            else if (correctas > incorrectas)
            {
                textoResultado.text = $"¡Bien hecho!\n{correctas}/{totales} correctas";
                textoResultado.color = Color.yellow;
            }
            else if (correctas > 0)
            {
                textoResultado.text = $"Puedes mejorar\n{correctas}/{totales} correctas";
                textoResultado.color = new Color(1f, 0.5f, 0f);
            }
            else
            {
                textoResultado.text = $"Sigue practicando\n0/{totales} correctas";
                textoResultado.color = Color.red;
            }
        }

        if (imagenResultado != null)
        {
            imagenResultado.sprite = (correctas == totales) ? spriteCorrecta : spriteIncorrecta;
        }

        // CAMBIO: Usar precisión del tracker para consistencia
        if (textoMetricasRonda != null)
        {
            var tracker = GameManager.Instance?.performanceTracker;
            float precision = tracker?.CalcularPrecisionGlobal() ?? 0f;
            string dificultad = GameManager.Instance?.difficultyManager?.DificultadActual.ToString() ?? "--";
            int rondaActual = (GameManager.Instance?.roundManager?.RondaActual ?? 0) + 1;
            float tiempoPromedio = tracker?.ObtenerMetricasGlobales().tiempoPromedioRespuesta ?? 0f;
            
            textoMetricasRonda.text = $"Ronda: {rondaActual}\n" +
                                      $"Dificultad: {dificultad}\n" +
                                      $"Precisión: {precision:F0}%\n" +
                                      $"Tiempo promedio: {tiempoPromedio:F1}s";
        }

        // Ya no necesitamos llamar ActualizarMetricas() aquí, se actualiza en tiempo real
    }

    void ActualizarProgreso(int identificadas, int totales)
    {
        if (textoProgreso != null)
        {
            int correctas = GameManager.Instance?.roundManager?.SenalesCorrectas ?? 0;
            textoProgreso.text = $"Progreso: {identificadas}/{totales} | Correctas: {correctas}";
        }
    }

    void ActualizarTemporizador(float tiempoRestante)
    {
        if (textoTemporizador != null)
        {
            // Formatear como 00:00 (Minutos:Segundos)
            int minutos = Mathf.FloorToInt(tiempoRestante / 60);
            int segundos = Mathf.FloorToInt(tiempoRestante % 60);
            textoTemporizador.text = string.Format("{0:00}:{1:00}", minutos, segundos);
        }
    }

    void MostrarAdvertenciaTiempo(float tiempoRestante)
    {
        if (textoTemporizador != null)
        {
            textoTemporizador.color = Color.red;
        }
    }

    private bool mostrandoMensajeIA = false;
    private NivelDificultad ultimaDificultad = NivelDificultad.Baja;
    private Coroutine corutinaMensajeIA; // NUEVO: Referencia a la corrutina

    void OnCambioDificultad(NivelDificultad nuevaDificultad)
    {
        ultimaDificultad = nuevaDificultad;

        // CAMBIO: Siempre actualizar, incluso si hay mensaje IA mostrándose
        // El mensaje IA ahora va en un campo separado
        ActualizarVisualDificultad(nuevaDificultad);
        
        Debug.Log($"UIManager: Dificultad cambiada a {nuevaDificultad}");
    }

    void ActualizarVisualDificultad(NivelDificultad dificultad)
    {
        if (textoDificultad != null)
        {
            textoDificultad.text = $"Dificultad: {dificultad}";
            
            textoDificultad.color = dificultad switch
            {
                NivelDificultad.Baja => Color.green,
                NivelDificultad.Media => Color.yellow,
                NivelDificultad.Alta => Color.red,
                _ => Color.white
            };
        }
    }

    // CAMBIO: El mensaje de IA ahora va a un campo separado
    void MostrarMensajeModeloIA(string mensaje)
    {
        // Cancelar corrutina anterior si existe
        if (corutinaMensajeIA != null)
        {
            StopCoroutine(corutinaMensajeIA);
        }
        
        corutinaMensajeIA = StartCoroutine(RutinaMostrarMensajeIA(mensaje));
        
        Debug.Log($"[UIManager] Mostrando mensaje IA: {mensaje}");
    }

    System.Collections.IEnumerator RutinaMostrarMensajeIA(string mensaje)
    {
        mostrandoMensajeIA = true;

        // CAMBIO: Usar campo separado para mensajes de IA
        if (textoMensajeIA != null)
        {
            textoMensajeIA.text = mensaje;
            textoMensajeIA.color = new Color(0.4f, 0.8f, 1f); // Azul Cyan para IA
            textoMensajeIA.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(4f);

        // Ocultar mensaje de IA
        if (textoMensajeIA != null)
        {
            textoMensajeIA.text = "";
            textoMensajeIA.gameObject.SetActive(false);
        }

        mostrandoMensajeIA = false;
        corutinaMensajeIA = null;
    }

    void MostrarEstadisticasFinales()
    {
        // CAMBIO: Usar métricas del juego completo, no solo de la última zona
        var tracker = GameManager.Instance?.performanceTracker;
        if (tracker == null) return;

        var metricasJuego = tracker.ObtenerMetricasJuegoCompleto();
        float precisionTotal = tracker.CalcularPrecisionJuegoCompleto();
        
        // Actualizar textos con métricas GLOBALES
        if (textoAciertos != null)
        {
            textoAciertos.text = $"Aciertos: {metricasJuego.aciertos} | Errores: {metricasJuego.errores}";
        }
        
        if (textoTasaAciertos != null)
        {
            textoTasaAciertos.text = $"Precisión Total: {precisionTotal:F0}%";
            
            // Color según rendimiento
            if (precisionTotal >= 80f)
                textoTasaAciertos.color = Color.green;
            else if (precisionTotal >= 50f)
                textoTasaAciertos.color = Color.yellow;
            else
                textoTasaAciertos.color = Color.red;
        }

        if (textoTiempoPromedio != null)
        {
            if (metricasJuego.tiempoPromedioRespuesta > 0)
                textoTiempoPromedio.text = $"Tiempo Promedio: {metricasJuego.tiempoPromedioRespuesta:F1}s";
            else
                textoTiempoPromedio.text = "Tiempo: --";
        }

        // NUEVO: Mostrar zonas completadas si hay un campo para ello
        int zonasCompletadas = (GameManager.Instance?.zoneManager?.ZonaActual ?? 0) + 1;
        int totalZonas = GameManager.Instance?.zoneManager?.TotalZonas ?? 1;
        
        Debug.Log($"UIManager: Estadísticas finales - Aciertos: {metricasJuego.aciertos}, Errores: {metricasJuego.errores}, Precisión: {precisionTotal:F1}%, Zonas: {zonasCompletadas}/{totalZonas}");
    }

    void OnBotonIniciarClick()
    {
        Debug.Log("UIManager: Botón Iniciar presionado");
        GameManager.Instance?.OnBotonIniciarPresionado();
    }

    // NUEVO: Método para el botón de volver al menú
    void OnBotonVolverMenuClick()
    {
        Debug.Log("UIManager: Botón Volver al Menú presionado");
        GameManager.Instance?.VolverAlMenuPrincipal();
    }
}
