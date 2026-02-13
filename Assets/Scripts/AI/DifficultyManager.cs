using UnityEngine;
using UnityEngine.Events;

public enum NivelDificultad
{
    Baja,
    Media,
    Alta
}

[System.Serializable]
public class ConfiguracionDificultad
{
    public int cantidadSenales;
    public float tiempoSegundos;
    public bool mostrarAyudaVisual;
    public bool incluirDistractores;
    public bool permitirRepeticionSenales; // Nuevo: controla si pueden repetirse señales
}

public class DifficultyManager : MonoBehaviour
{
    [Header("Estado Actual")]
    [SerializeField] private NivelDificultad dificultadActual = NivelDificultad.Baja;
    public NivelDificultad DificultadActual => dificultadActual;

    [Header("Configuraciones por Dificultad")]
    public ConfiguracionDificultad configBaja = new ConfiguracionDificultad
    {
        cantidadSenales = 3,
        tiempoSegundos = 5f, // CAMBIO: Tiempo para responder 1 señal
        mostrarAyudaVisual = true,
        incluirDistractores = false,
        permitirRepeticionSenales = false // Nunca en baja
    };
    
    public ConfiguracionDificultad configMedia = new ConfiguracionDificultad
    {
        cantidadSenales = 5,
        tiempoSegundos = 3.5f, // CAMBIO: Más rápido
        mostrarAyudaVisual = false,
        incluirDistractores = true,
        permitirRepeticionSenales = false // No en media
    };
    
    public ConfiguracionDificultad configAlta = new ConfiguracionDificultad
    {
        cantidadSenales = 7,
        tiempoSegundos = 2f, // CAMBIO: Muy rápido
        mostrarAyudaVisual = false,
        incluirDistractores = true,
        permitirRepeticionSenales = true // Solo en alta como distractores
    };

    [Header("Eventos")]
    public UnityEvent<NivelDificultad> OnDificultadCambiada;
    public UnityEvent<string> OnMensajeModeloIA; // Para mostrar predicción del modelo

    [Header("Estado de Predicción IA")]
    [SerializeField] private string ultimoMensajeModelo = ""; // NUEVO
    public string UltimoMensajeModelo => ultimoMensajeModelo;

    [Header("Modelo de Machine Learning")]
    public AIServiceClient aiClient;
    public bool usarModeloML = true;  // Siempre usar ML, si falla no ajusta

    // FIX: Forzar valores por código al iniciar para evitar que el Inspector guarde valores antiguos (como los 25s)
    void Awake()
    {
        ForzarValoresPorDefecto();
    }

    void OnValidate()
    {
        // Útil para ver los cambios reflejados en el editor mientras programas
        ForzarValoresPorDefecto(); 
    }

    void ForzarValoresPorDefecto()
    {
        // FORZAR cantidad de señales por dificultad
        configBaja.cantidadSenales = 3;
        configMedia.cantidadSenales = 5;
        configAlta.cantidadSenales = 7;
        
        // AJUSTE: Tiempos más generosos para mejor experiencia
        configBaja.tiempoSegundos = 12f;   // 12s para aprender
        configMedia.tiempoSegundos = 8f;   // 8s 
        configAlta.tiempoSegundos = 5f;    // 5s (desafiante pero justo)
        
        // Asegurar configuraciones críticas
        configBaja.permitirRepeticionSenales = false;
        configMedia.permitirRepeticionSenales = false;
        configAlta.permitirRepeticionSenales = true;
        
        Debug.Log($"[DifficultyManager] Configuraciones forzadas: Baja={configBaja.cantidadSenales}, Media={configMedia.cantidadSenales}, Alta={configAlta.cantidadSenales}");
    }

    void Start()
    {
        // NUEVO: Suscribirse a eventos del cliente IA
        if (aiClient != null)
        {
            aiClient.OnPrediccionRecibida += OnPrediccionModeloRecibida;
        }
    }

    void OnDestroy()
    {
        // NUEVO: Desuscribirse
        if (aiClient != null)
        {
            aiClient.OnPrediccionRecibida -= OnPrediccionModeloRecibida;
        }
    }

    void OnPrediccionModeloRecibida(NivelDificultad prediccion, string descripcion)
    {
        ultimoMensajeModelo = $"🤖 Modelo IA: {descripcion}";
        Debug.Log($"[DifficultyManager] {ultimoMensajeModelo}");
        
        // Notificar a la UI
        OnMensajeModeloIA?.Invoke(ultimoMensajeModelo);
    }

    public void EstablecerDificultad(NivelDificultad nivel)
    {
        if (dificultadActual != nivel)
        {
            NivelDificultad anterior = dificultadActual;
            dificultadActual = nivel;
            OnDificultadCambiada?.Invoke(nivel);
            Debug.Log($"Dificultad cambiada a: {nivel}");
        }
    }

    public ConfiguracionDificultad ObtenerConfiguracion()
    {
        return dificultadActual switch
        {
            NivelDificultad.Baja => configBaja,
            NivelDificultad.Media => configMedia,
            NivelDificultad.Alta => configAlta,
            _ => configBaja
        };
    }

    public void EvaluarYAjustar(MetricasRecientes metricas)
    {
        // Versión sin callback - no espera respuesta
        EvaluarYAjustar(metricas, null);
    }

    /// <summary>
    /// Evalúa y ajusta dificultad con callback para saber cuándo terminó
    /// </summary>
    public void EvaluarYAjustar(MetricasRecientes metricas, System.Action onCompleto)
    {
        Debug.Log($"[DifficultyManager] === EVALUANDO DIFICULTAD ===");
        Debug.Log($"[DifficultyManager] Métricas: Intentos={metricas.intentosTotales}, Aciertos={metricas.aciertos}, Tasa={metricas.tasaAciertos:P0}");
        Debug.Log($"[DifficultyManager] Dificultad actual: {dificultadActual} ({ObtenerConfiguracion().cantidadSenales} señales)");
        
        if (usarModeloML && aiClient != null && aiClient.EstaConectado)
        {
            Debug.Log("[DifficultyManager] Solicitando predicción al modelo ML...");
            aiClient.SolicitarAjusteDificultad(metricas, (nuevaDificultad) => {
                OnRespuestaIA(nuevaDificultad);
                onCompleto?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("[DifficultyManager] Modelo ML no disponible. Dificultad sin cambios.");
            onCompleto?.Invoke();
        }
    }

    void OnRespuestaIA(NivelDificultad recomendacion)
    {
        NivelDificultad anterior = dificultadActual;
        
        Debug.Log($"[DifficultyManager] ========================================");
        Debug.Log($"[DifficultyManager] PREDICCIÓN RECIBIDA DEL MODELO ML");
        Debug.Log($"[DifficultyManager] Dificultad ANTERIOR: {anterior} ({ObtenerConfiguracionPorNivel(anterior).cantidadSenales} señales)");
        Debug.Log($"[DifficultyManager] Dificultad NUEVA: {recomendacion} ({ObtenerConfiguracionPorNivel(recomendacion).cantidadSenales} señales)");
        Debug.Log($"[DifficultyManager] ========================================");
        
        EstablecerDificultad(recomendacion);
        
        // NUEVO: Registrar el ajuste en el servidor de métricas
        if (anterior != recomendacion)
        {
            var metricas = GameManager.Instance?.performanceTracker?.ObtenerMetricasRecientes();
            int zona = GameManager.Instance?.zoneManager?.ZonaActual ?? 0;
            int ronda = GameManager.Instance?.roundManager?.RondaActual ?? 0;
            
            MetricsClient.Instance?.RegistrarAjusteDificultad(
                (int)anterior,
                (int)recomendacion,
                "modelo_ia",
                metricas?.tasaAciertos ?? 0,
                metricas?.tiempoPromedioRespuesta ?? 0,
                zona,
                ronda
            );
        }
    }

    /// <summary>
    /// Ajusta la dificultad al entrar en una nueva zona, respetando límites de la zona
    /// </summary>
    public void AjustarDificultadParaZona(int indiceZona, ZonaData zonaData)
    {
        if (zonaData == null) return;
        
        NivelDificultad dificultadActualTemp = dificultadActual;
        
        // Limitar dificultad a los rangos de la zona
        if (dificultadActualTemp < zonaData.dificultadMinima)
        {
            dificultadActualTemp = zonaData.dificultadMinima;
        }
        else if (dificultadActualTemp > zonaData.dificultadMaxima)
        {
            // Reset parcial: bajar a la máxima permitida de la zona
            dificultadActualTemp = zonaData.dificultadMaxima;
        }
        
        EstablecerDificultad(dificultadActualTemp);
        Debug.Log($"Dificultad ajustada para zona {indiceZona}: {dificultadActualTemp} (rango: {zonaData.dificultadMinima}-{zonaData.dificultadMaxima})");
    }

    /// <summary>
    /// Obtiene configuración por nivel específico (helper para debug)
    /// </summary>
    public ConfiguracionDificultad ObtenerConfiguracionPorNivel(NivelDificultad nivel)
    {
        return nivel switch
        {
            NivelDificultad.Baja => configBaja,
            NivelDificultad.Media => configMedia,
            NivelDificultad.Alta => configAlta,
            _ => configBaja
        };
    }

    /// <summary>
    /// Verifica si la dificultad actual permite repetir señales
    /// </summary>
    public bool PermiteRepeticionSenales()
    {
        return ObtenerConfiguracion().permitirRepeticionSenales;
    }
}
