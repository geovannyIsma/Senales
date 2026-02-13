using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class ErrorData
{
    public string nombreSenal;
    public string respuestaUsuario;
    public float tiempoRespuesta;
    public NivelDificultad dificultad;
    public int zonaActual;
    public int intentosPrevios;
    public float timestamp;
    
    // Datos adicionales para análisis
    public bool fueCorregidoPosteriormente;
    public string feedbackRecibido;
}

/// <summary>
/// Rastrea y registra los errores del usuario para generar retroalimentación personalizada
/// </summary>
public class ErrorTracker : MonoBehaviour
{
    [Header("Historial de Errores")]
    [SerializeField] private List<ErrorData> historialErrores = new List<ErrorData>();
    [SerializeField] private List<ErrorData> erroresSesionActual = new List<ErrorData>();

    [Header("Eventos")]
    public UnityEvent<ErrorData> OnErrorDetectado;
    public UnityEvent<FeedbackResponse> OnFeedbackListo;

    [Header("Referencias")]
    public FeedbackAIClient feedbackClient;

    // Diccionario para rastrear errores por señal
    private Dictionary<string, int> contadorErroresPorSenal = new Dictionary<string, int>();

    void Awake()
    {
        if (feedbackClient == null)
        {
            feedbackClient = FindFirstObjectByType<FeedbackAIClient>();
        }
    }

    /// <summary>
    /// Registra un error y solicita retroalimentación de la IA
    /// </summary>
    public void RegistrarError(TrafficSign senalCorrecta, TrafficSignData respuestaUsuario, float tiempoRespuesta)
    {
        if (senalCorrecta == null || senalCorrecta.datos == null) return;

        // Contar errores previos con esta señal
        string nombreSenal = senalCorrecta.datos.nombreSenal;
        if (!contadorErroresPorSenal.ContainsKey(nombreSenal))
        {
            contadorErroresPorSenal[nombreSenal] = 0;
        }
        contadorErroresPorSenal[nombreSenal]++;

        int zonaActual = GameManager.Instance?.zoneManager?.ZonaActual ?? 0;
        NivelDificultad dificultadActual = GameManager.Instance?.difficultyManager?.DificultadActual ?? NivelDificultad.Baja;

        // Crear registro del error
        ErrorData errorData = new ErrorData
        {
            nombreSenal = nombreSenal,
            respuestaUsuario = respuestaUsuario?.nombreSenal ?? "Tiempo agotado",
            tiempoRespuesta = tiempoRespuesta,
            dificultad = dificultadActual,
            zonaActual = zonaActual,
            intentosPrevios = contadorErroresPorSenal[nombreSenal],
            timestamp = Time.time,
            fueCorregidoPosteriormente = false,
            feedbackRecibido = ""
        };

        // Guardar en historiales
        historialErrores.Add(errorData);
        erroresSesionActual.Add(errorData);

        Debug.Log($"ErrorTracker: Error registrado - señal '{nombreSenal}' (intento #{errorData.intentosPrevios})");

        // NUEVO: Enviar error al servidor de métricas
        string tipoError = respuestaUsuario == null ? "tiempo_agotado" : "confusion";
        MetricsClient.Instance?.RegistrarErrorDetallado(
            nombreSenal,
            errorData.respuestaUsuario,
            tipoError,
            tiempoRespuesta,
            zonaActual,
            (int)dificultadActual,
            errorData.intentosPrevios,
            null // El feedback se agregará después
        );

        // Notificar
        OnErrorDetectado?.Invoke(errorData);

        // Solicitar retroalimentación de la IA
        SolicitarFeedbackIA(errorData);
    }

    /// <summary>
    /// Registra un error por tiempo agotado
    /// </summary>
    public void RegistrarErrorTiempoAgotado(TrafficSign senal)
    {
        // CAMBIO: Usar el tiempo configurado del temporizador como tiempo de respuesta
        // porque el tiempo "agotado" significa que usó todo el tiempo disponible
        float tiempoConfig = GameManager.Instance?.difficultyManager?.ObtenerConfiguracion()?.tiempoSegundos ?? 5f;
        
        // Llamar a RegistrarError con el tiempo del temporizador
        RegistrarError(senal, null, tiempoConfig);
    }

    void SolicitarFeedbackIA(ErrorData errorData)
    {
        if (feedbackClient == null)
        {
            Debug.LogWarning("ErrorTracker: No hay FeedbackAIClient configurado");
            return;
        }

        feedbackClient.SolicitarFeedback(errorData, (response) =>
        {
            // Guardar el feedback recibido
            errorData.feedbackRecibido = response.mensaje_completo;
            
            // Notificar que el feedback está listo
            OnFeedbackListo?.Invoke(response);
            
            Debug.Log($"ErrorTracker: Feedback recibido para '{errorData.nombreSenal}'");
        });
    }

    /// <summary>
    /// Marca un error como corregido cuando el usuario acierta posteriormente
    /// </summary>
    public void MarcarErrorCorregido(string nombreSenal)
    {
        foreach (var error in erroresSesionActual)
        {
            if (error.nombreSenal == nombreSenal && !error.fueCorregidoPosteriormente)
            {
                error.fueCorregidoPosteriormente = true;
                Debug.Log($"ErrorTracker: Error corregido para '{nombreSenal}'");
                break;
            }
        }
    }

    public List<ErrorData> ObtenerHistorialCompleto() => new List<ErrorData>(historialErrores);
    public List<ErrorData> ObtenerErroresSesion() => new List<ErrorData>(erroresSesionActual);
    public int TotalErroresSesion => erroresSesionActual.Count;

    public void ReiniciarSesion()
    {
        erroresSesionActual.Clear();
        contadorErroresPorSenal.Clear();
    }
}
