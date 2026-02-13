using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

[Serializable]
public class AIRequest
{
    public int zona;
    public int senales_mostradas;
    public int aciertos;
    public int errores;
    public float tiempo_promedio;
}

[Serializable]
public class AIResponse
{
    public int dificultad;
    public string descripcion;
}

public class AIServiceClient : MonoBehaviour
{
    [Header("Configuración del Servidor")]
    public string urlServidor = "http://127.0.0.1:8000";
    public string endpointPredecir = "/predecir";
    
    [Header("Estado")]
    [SerializeField] private bool conectado = false;
    [SerializeField] private string ultimaPrediccionDescripcion = ""; // NUEVO
    public bool EstaConectado => conectado;
    public string UltimaPrediccion => ultimaPrediccionDescripcion; // NUEVO

    // NUEVO: Evento para notificar predicción con descripción
    public event Action<NivelDificultad, string> OnPrediccionRecibida;

    private Action<NivelDificultad> callbackActual;

    void Start()
    {
        StartCoroutine(VerificarConexion());
    }

    IEnumerator VerificarConexion()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(urlServidor + "/"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            conectado = request.result == UnityWebRequest.Result.Success;
            Debug.Log(conectado ? "Conexión con IA establecida" : "IA no disponible, usando lógica local");
        }
    }

    public void SolicitarAjusteDificultad(MetricasRecientes metricas, Action<NivelDificultad> callback)
    {
        if (!conectado)
        {
            Debug.LogWarning("IA no conectada, no se puede solicitar ajuste");
            return;
        }

        callbackActual = callback;
        
        // Obtener zona actual del GameManager
        int zonaActual = GameManager.Instance?.zoneManager?.ZonaActual ?? 0;
        
        StartCoroutine(EnviarSolicitud(metricas, zonaActual));
    }

    IEnumerator EnviarSolicitud(MetricasRecientes metricas, int zona)
    {
        // Calcular errores a partir de intentos y aciertos
        int errores = metricas.intentosTotales - metricas.aciertos;
        
        // Obtener cantidad de señales de la configuración actual
        int senalesMostradas = GameManager.Instance?.difficultyManager?.ObtenerConfiguracion()?.cantidadSenales ?? 3;

        AIRequest requestData = new AIRequest
        {
            zona = zona,
            senales_mostradas = senalesMostradas,
            aciertos = metricas.aciertos,
            errores = errores,
            tiempo_promedio = metricas.tiempoPromedioRespuesta
        };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log($"[IA-ML] Enviando a modelo: {jsonData}");
        
        using (UnityWebRequest request = new UnityWebRequest(urlServidor + endpointPredecir, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[IA-ML] Respuesta del modelo: {responseText}");
                
                AIResponse response = JsonUtility.FromJson<AIResponse>(responseText);
                
                NivelDificultad recomendacion = ConvertirDificultad(response.dificultad);
                
                // NUEVO: Guardar y notificar la descripción
                ultimaPrediccionDescripcion = response.descripcion;
                Debug.Log($"[IA-ML] Modelo predijo: {recomendacion} - {response.descripcion}");
                
                // NUEVO: Notificar con descripción
                OnPrediccionRecibida?.Invoke(recomendacion, response.descripcion);
                
                callbackActual?.Invoke(recomendacion);
            }
            else
            {
                Debug.LogError($"[IA-ML] Error en solicitud: {request.error}");
                ultimaPrediccionDescripcion = "Error de conexión";
            }
        }
    }

    NivelDificultad ConvertirDificultad(int valor)
    {
        return valor switch
        {
            0 => NivelDificultad.Baja,
            1 => NivelDificultad.Media,
            2 => NivelDificultad.Alta,
            _ => NivelDificultad.Media
        };
    }

    // Método de prueba manual
    public void ProbarConexion()
    {
        StartCoroutine(VerificarConexion());
    }

    // Enviar métricas completas al final de sesión
    public void EnviarMetricasCompletas(PerformanceTracker tracker)
    {
        if (!conectado) return;
        StartCoroutine(EnviarHistorial(tracker.ObtenerHistorialCompleto()));
    }

    IEnumerator EnviarHistorial(System.Collections.Generic.List<RegistroIntento> historial)
    {
        // Implementar si tu backend tiene endpoint para guardar historial
        yield return null;
    }
}
