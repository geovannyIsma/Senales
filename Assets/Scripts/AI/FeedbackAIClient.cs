using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

[Serializable]
public class FeedbackRequest
{
    public string nombre_senal;
    public string respuesta_usuario;
    public float tiempo_respuesta;
    public int nivel_dificultad;
    public int zona_actual;
    public int intentos_previos;
}

[Serializable]
public class FeedbackResponse
{
    public bool success;
    public string significado;
    public string motivo_error;
    public string ejemplo_real;
    public string mnemotecnia;
    public string mensaje_completo;
    public string error_message;
}

/// <summary>
/// Cliente para solicitar retroalimentación pedagógica generada por IA (GPT-4, Gemini, Llama)
/// </summary>
public class FeedbackAIClient : MonoBehaviour
{
    [Header("Configuración del Servidor")]
    public string urlServidor = "http://127.0.0.1:8000";
    public string endpointFeedback = "/generar_feedback";
    
    [Header("Estado")]
    [SerializeField] private bool conectado = false;
    [SerializeField] private bool esperandoRespuesta = false;
    
    public bool EstaConectado => conectado;
    public bool EstaOcupado => esperandoRespuesta;

    [Header("Configuración de Timeout")]
    public int timeoutSegundos = 30;

    [Header("Fallback")]
    [TextArea(3, 5)]
    public string mensajeFallbackGenerico = "Revisa el significado de esta señal. Parece que la clasificación no fue correcta. ¡Sigue practicando!";

    // Eventos
    public event Action<FeedbackResponse> OnFeedbackRecibido;
    public event Action<string> OnFeedbackError;

    void Start()
    {
        StartCoroutine(VerificarConexion());
    }

    IEnumerator VerificarConexion()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(urlServidor + "/health"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            conectado = request.result == UnityWebRequest.Result.Success;
            Debug.Log(conectado ? 
                "FeedbackAIClient: Conexión con servidor de IA establecida" : 
                "FeedbackAIClient: Servidor de IA no disponible, se usará fallback");
        }
    }

    /// <summary>
    /// Solicita retroalimentación pedagógica para un error cometido
    /// </summary>
    public void SolicitarFeedback(ErrorData errorData, Action<FeedbackResponse> callback)
    {
        if (esperandoRespuesta)
        {
            Debug.LogWarning("FeedbackAIClient: Ya hay una solicitud en progreso");
            return;
        }

        if (!conectado)
        {
            Debug.LogWarning("FeedbackAIClient: Servidor no conectado, usando fallback");
            var fallback = GenerarFeedbackFallback(errorData);
            callback?.Invoke(fallback);
            OnFeedbackRecibido?.Invoke(fallback);
            return;
        }

        StartCoroutine(EnviarSolicitudFeedback(errorData, callback));
    }

    IEnumerator EnviarSolicitudFeedback(ErrorData errorData, Action<FeedbackResponse> callback)
    {
        esperandoRespuesta = true;

        FeedbackRequest request = new FeedbackRequest
        {
            nombre_senal = errorData.nombreSenal,
            respuesta_usuario = errorData.respuestaUsuario,
            tiempo_respuesta = errorData.tiempoRespuesta,
            nivel_dificultad = (int)errorData.dificultad,
            zona_actual = errorData.zonaActual,
            intentos_previos = errorData.intentosPrevios
        };

        string jsonData = JsonUtility.ToJson(request);
        Debug.Log($"FeedbackAIClient: Enviando solicitud: {jsonData}");

        using (UnityWebRequest webRequest = new UnityWebRequest(urlServidor + endpointFeedback, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = timeoutSegundos;

            yield return webRequest.SendWebRequest();

            esperandoRespuesta = false;

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseText = webRequest.downloadHandler.text;
                Debug.Log($"FeedbackAIClient: Respuesta recibida: {responseText}");

                FeedbackResponse response = JsonUtility.FromJson<FeedbackResponse>(responseText);
                
                if (response.success)
                {
                    callback?.Invoke(response);
                    OnFeedbackRecibido?.Invoke(response);
                }
                else
                {
                    Debug.LogWarning($"FeedbackAIClient: Error del servidor: {response.error_message}");
                    var fallback = GenerarFeedbackFallback(errorData);
                    callback?.Invoke(fallback);
                    OnFeedbackError?.Invoke(response.error_message);
                }
            }
            else
            {
                Debug.LogError($"FeedbackAIClient: Error de conexión: {webRequest.error}");
                var fallback = GenerarFeedbackFallback(errorData);
                callback?.Invoke(fallback);
                OnFeedbackError?.Invoke(webRequest.error);
            }
        }
    }

    /// <summary>
    /// Genera una respuesta de fallback cuando la IA no está disponible
    /// </summary>
    FeedbackResponse GenerarFeedbackFallback(ErrorData errorData)
    {
        return new FeedbackResponse
        {
            success = false,
            significado = $"La señal '{errorData.nombreSenal}' es una señal de tránsito importante que debes conocer.",
            motivo_error = "No se pudo determinar el motivo exacto del error.",
            ejemplo_real = "Imagina que vas conduciendo y encuentras esta señal. ¿Qué harías?",
            mnemotecnia = "Recuerda: cada señal tiene un propósito específico para tu seguridad.",
            mensaje_completo = mensajeFallbackGenerico,
            error_message = "Servicio de IA no disponible"
        };
    }

    /// <summary>
    /// Verifica la conexión con el servidor
    /// </summary>
    public void ReintentarConexion()
    {
        StartCoroutine(VerificarConexion());
    }
}
