using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;

[Serializable]
public class CrearSesionRequest
{
    public int estudiante_id;
    public int dificultad_inicial;
}

[Serializable]
public class CrearSesionResponse
{
    public int sesion_id;
    public string mensaje;
}

[Serializable]
public class ActualizarSesionRequest
{
    public int total_aciertos;
    public int total_errores;
    public float tiempo_promedio_respuesta;
    public int zonas_completadas;
    public int zona_maxima_alcanzada;
    public int dificultad_final;
    public bool completada;
}

[Serializable]
public class RegistrarIntentoRequest
{
    public int sesion_id;
    public string nombre_senal;
    public string respuesta_usuario;
    public bool fue_correcta;
    public float tiempo_respuesta;
    public int zona;
    public int ronda;
    public int dificultad;
}

[Serializable]
public class RegistrarErrorRequest
{
    public int sesion_id;
    public string nombre_senal;
    public string respuesta_usuario;
    public string tipo_error;
    public float tiempo_respuesta;
    public int zona;
    public int dificultad;
    public int intentos_previos;
    public string feedback_generado;
}

[Serializable]
public class RegistrarAjusteRequest
{
    public int sesion_id;
    public int dificultad_anterior;
    public int dificultad_nueva;
    public string motivo;
    public float tasa_aciertos;
    public float tiempo_promedio;
    public int zona;
    public int ronda;
}

[Serializable]
public class ConfiguracionResponse
{
    public int senales_dificultad_baja;
    public int senales_dificultad_media;
    public int senales_dificultad_alta;
    public float tiempo_dificultad_baja;
    public float tiempo_dificultad_media;
    public float tiempo_dificultad_alta;
    public int dificultad_inicial;
    public int rondas_por_zona;
    public float tasa_aciertos_minima;
    public float umbral_subir_dificultad;
    public float umbral_bajar_dificultad;
    public bool mostrar_ayuda_visual_baja;
    public bool incluir_distractores_media;
    public bool incluir_distractores_alta;
}

/// <summary>
/// Cliente para enviar métricas y recibir configuración del servidor
/// </summary>
public class MetricsClient : MonoBehaviour
{
    public static MetricsClient Instance { get; private set; }

    [Header("Configuración del Servidor")]
    public string urlServidor = "http://127.0.0.1:8000";
    
    [Header("Identificación")]
    public int estudianteId = 1; // Configurable por sesión
    public string nombreEstudiante = "Estudiante VR";
    
    [Header("Estado")]
    [SerializeField] private int sesionActualId = -1;
    [SerializeField] private bool conectado = false;
    [SerializeField] private bool verificandoConexion = false; // NUEVO
    
    public int SesionActualId => sesionActualId;
    public bool EstaConectado => conectado;

    [Header("Configuración Cargada")]
    [SerializeField] private ConfiguracionResponse configuracionActual;
    public ConfiguracionResponse ConfiguracionActual => configuracionActual;

    // Eventos
    public event Action<int> OnSesionCreada;
    public event Action<ConfiguracionResponse> OnConfiguracionCargada;

    // Cola de peticiones para enviar
    private Queue<IEnumerator> colaPeticiones = new Queue<IEnumerator>();
    private bool procesandoCola = false;

    // NUEVO: Cola de intentos pendientes (cuando la sesión aún no existe)
    private List<RegistrarIntentoRequest> intentosPendientes = new List<RegistrarIntentoRequest>();
    private List<RegistrarErrorRequest> erroresPendientes = new List<RegistrarErrorRequest>();
    private bool sesionEnCreacion = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log($"[MetricsClient] Inicializado - URL: {urlServidor}");
    }

    void Start()
    {
        StartCoroutine(VerificarConexion());
    }

    IEnumerator VerificarConexion()
    {
        if (verificandoConexion) yield break;
        verificandoConexion = true;
        
        Debug.Log($"[MetricsClient] Verificando conexión a {urlServidor}/health...");
        
        using (UnityWebRequest request = UnityWebRequest.Get(urlServidor + "/health"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            conectado = request.result == UnityWebRequest.Result.Success;
            
            if (conectado)
            {
                Debug.Log($"[MetricsClient] ✓ Conexión establecida. Respuesta: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogError($"[MetricsClient] ✗ Error de conexión: {request.error}");
                Debug.LogError($"[MetricsClient] Asegúrate de que el servidor esté corriendo en {urlServidor}");
            }
        }

        if (conectado)
        {
            yield return CargarConfiguracionCoroutine();
        }
        
        verificandoConexion = false;
    }

    // ============== SESIONES ==============

    /// <summary>
    /// Inicia una nueva sesión de juego
    /// </summary>
    public void IniciarSesion(int dificultadInicial = 0)
    {
        Debug.Log($"[MetricsClient] IniciarSesion llamado - Conectado: {conectado}, SesionActual: {sesionActualId}, EnCreacion: {sesionEnCreacion}");
        
        if (sesionEnCreacion)
        {
            Debug.LogWarning("[MetricsClient] Ya hay una sesión en proceso de creación");
            return;
        }
        
        if (!conectado)
        {
            Debug.LogWarning("[MetricsClient] No hay conexión. Reintentando...");
            StartCoroutine(ReintentarYCrearSesion(dificultadInicial));
            return;
        }

        if (sesionActualId >= 0)
        {
            Debug.LogWarning($"[MetricsClient] Ya existe una sesión activa: {sesionActualId}");
            return;
        }

        StartCoroutine(CrearSesionCoroutine(dificultadInicial));
    }

    IEnumerator ReintentarYCrearSesion(int dificultadInicial)
    {
        yield return VerificarConexion();
        
        if (conectado)
        {
            yield return CrearSesionCoroutine(dificultadInicial);
        }
    }

    IEnumerator CrearSesionCoroutine(int dificultadInicial)
    {
        sesionEnCreacion = true; // NUEVO: Marcar que estamos creando sesión
        
        CrearSesionRequest datos = new CrearSesionRequest
        {
            estudiante_id = estudianteId,
            dificultad_inicial = dificultadInicial
        };

        string json = JsonUtility.ToJson(datos);
        Debug.Log($"[MetricsClient] Creando sesión: {json}");
        
        using (UnityWebRequest request = new UnityWebRequest(urlServidor + "/sesiones", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"[MetricsClient] ✓ Respuesta servidor: {responseText}");
                
                var response = JsonUtility.FromJson<CrearSesionResponse>(responseText);
                sesionActualId = response.sesion_id;
                Debug.Log($"[MetricsClient] ✓ Sesión creada con ID: {sesionActualId}");
                OnSesionCreada?.Invoke(sesionActualId);
                
                // NUEVO: Enviar intentos pendientes
                EnviarIntentosPendientes();
            }
            else
            {
                Debug.LogError($"[MetricsClient] ✗ Error creando sesión: {request.error}");
                Debug.LogError($"[MetricsClient] Código: {request.responseCode}, Respuesta: {request.downloadHandler?.text}");
            }
        }
        
        sesionEnCreacion = false; // NUEVO: Ya terminamos de crear
    }

    // NUEVO: Enviar intentos que se acumularon mientras se creaba la sesión
    void EnviarIntentosPendientes()
    {
        Debug.Log($"[MetricsClient] Enviando {intentosPendientes.Count} intentos pendientes y {erroresPendientes.Count} errores pendientes");
        
        foreach (var intento in intentosPendientes)
        {
            intento.sesion_id = sesionActualId; // Actualizar con el ID correcto
            EnviarPeticion(RegistrarIntentoCoroutine(intento));
        }
        intentosPendientes.Clear();
        
        foreach (var error in erroresPendientes)
        {
            error.sesion_id = sesionActualId; // Actualizar con el ID correcto
            EnviarPeticion(RegistrarErrorCoroutine(error));
        }
        erroresPendientes.Clear();
    }

    /// <summary>
    /// Finaliza la sesión actual con las métricas finales
    /// </summary>
    public void FinalizarSesion(MetricasRecientes metricasFinales, int zonasCompletadas, int zonaMaxima, bool completada)
    {
        Debug.Log($"[MetricsClient] FinalizarSesion - SesionID: {sesionActualId}");
        
        if (sesionActualId < 0)
        {
            Debug.LogWarning("[MetricsClient] No hay sesión activa para finalizar");
            return;
        }

        int dificultadFinal = (int)(GameManager.Instance?.difficultyManager?.DificultadActual ?? NivelDificultad.Baja);

        ActualizarSesionRequest datos = new ActualizarSesionRequest
        {
            total_aciertos = metricasFinales.aciertos,
            total_errores = metricasFinales.errores,
            tiempo_promedio_respuesta = metricasFinales.tiempoPromedioRespuesta,
            zonas_completadas = zonasCompletadas,
            zona_maxima_alcanzada = zonaMaxima,
            dificultad_final = dificultadFinal,
            completada = completada
        };

        Debug.Log($"[MetricsClient] Finalizando sesión con datos: aciertos={datos.total_aciertos}, errores={datos.total_errores}");
        
        // Enviar de forma síncrona para asegurar que se complete
        StartCoroutine(ActualizarSesionCoroutine(datos));
    }

    IEnumerator ActualizarSesionCoroutine(ActualizarSesionRequest datos)
    {
        string json = JsonUtility.ToJson(datos);
        string url = $"{urlServidor}/sesiones/{sesionActualId}";
        
        Debug.Log($"[MetricsClient] PUT {url}: {json}");
        
        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[MetricsClient] ✓ Sesión {sesionActualId} finalizada correctamente");
                sesionActualId = -1;
            }
            else
            {
                Debug.LogError($"[MetricsClient] ✗ Error finalizando sesión: {request.error}");
            }
        }
    }

    // ============== INTENTOS ==============

    /// <summary>
    /// Registra un intento de identificación de señal
    /// </summary>
    public void RegistrarIntento(string nombreSenal, string respuestaUsuario, bool fueCorrecta, 
                                  float tiempoRespuesta, int zona, int ronda, int dificultad)
    {
        if (!conectado)
        {
            Debug.LogWarning("[MetricsClient] Sin conexión, intento no registrado");
            return;
        }

        RegistrarIntentoRequest datos = new RegistrarIntentoRequest
        {
            sesion_id = sesionActualId,
            nombre_senal = nombreSenal,
            respuesta_usuario = respuestaUsuario ?? "",
            fue_correcta = fueCorrecta,
            tiempo_respuesta = tiempoRespuesta,
            zona = zona,
            ronda = ronda,
            dificultad = dificultad
        };

        // NUEVO: Si no hay sesión activa, encolar para después
        if (sesionActualId < 0)
        {
            Debug.LogWarning($"[MetricsClient] Sesión no activa, encolando intento: {nombreSenal}");
            intentosPendientes.Add(datos);
            return;
        }

        Debug.Log($"[MetricsClient] Registrando intento: {nombreSenal} - {(fueCorrecta ? "Correcto" : "Incorrecto")} - SesionID: {sesionActualId}");
        EnviarPeticion(RegistrarIntentoCoroutine(datos));
    }

    IEnumerator RegistrarIntentoCoroutine(RegistrarIntentoRequest datos)
    {
        string json = JsonUtility.ToJson(datos);
        Debug.Log($"[MetricsClient] POST /intentos: {json}"); // NUEVO: Log detallado
        
        using (UnityWebRequest request = new UnityWebRequest(urlServidor + "/intentos", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[MetricsClient] ✓ Intento registrado: {request.downloadHandler.text}");
            }
            else
            {
                Debug.LogWarning($"[MetricsClient] ✗ Error registrando intento: {request.error} - Respuesta: {request.downloadHandler?.text}");
            }
        }
    }

    // ============== ERRORES ==============

    /// <summary>
    /// Registra un error detallado para análisis
    /// </summary>
    public void RegistrarErrorDetallado(string nombreSenal, string respuestaUsuario, string tipoError,
                                         float tiempoRespuesta, int zona, int dificultad, 
                                         int intentosPrevios, string feedbackGenerado = null)
    {
        if (!conectado) 
        {
            Debug.LogWarning("[MetricsClient] No se puede registrar error: sin conexión");
            return;
        }

        RegistrarErrorRequest datos = new RegistrarErrorRequest
        {
            sesion_id = sesionActualId,
            nombre_senal = nombreSenal,
            respuesta_usuario = respuestaUsuario ?? "",
            tipo_error = tipoError,
            tiempo_respuesta = tiempoRespuesta,
            zona = zona,
            dificultad = dificultad,
            intentos_previos = intentosPrevios,
            feedback_generado = feedbackGenerado ?? ""
        };

        // NUEVO: Si no hay sesión activa, encolar para después
        if (sesionActualId < 0)
        {
            Debug.LogWarning($"[MetricsClient] Sesión no activa, encolando error: {nombreSenal}");
            erroresPendientes.Add(datos);
            return;
        }

        Debug.Log($"[MetricsClient] Registrando error: {nombreSenal} - {tipoError} - SesionID: {sesionActualId}");
        EnviarPeticion(RegistrarErrorCoroutine(datos));
    }

    IEnumerator RegistrarErrorCoroutine(RegistrarErrorRequest datos)
    {
        string json = JsonUtility.ToJson(datos);
        
        using (UnityWebRequest request = new UnityWebRequest(urlServidor + "/errores", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[MetricsClient] ✓ Error registrado");
            }
            else
            {
                Debug.LogWarning($"[MetricsClient] ✗ Error registrando error: {request.error}");
            }
        }
    }

    // ============== AJUSTES DE DIFICULTAD ==============

    /// <summary>
    /// Registra un ajuste de dificultad realizado por la IA
    /// </summary>
    public void RegistrarAjusteDificultad(int dificultadAnterior, int dificultadNueva, string motivo,
                                           float tasaAciertos, float tiempoPromedio, int zona, int ronda)
    {
        if (sesionActualId < 0 || !conectado) return;

        RegistrarAjusteRequest datos = new RegistrarAjusteRequest
        {
            sesion_id = sesionActualId,
            dificultad_anterior = dificultadAnterior,
            dificultad_nueva = dificultadNueva,
            motivo = motivo,
            tasa_aciertos = tasaAciertos,
            tiempo_promedio = tiempoPromedio,
            zona = zona,
            ronda = ronda
        };

        Debug.Log($"[MetricsClient] Registrando ajuste: {dificultadAnterior} -> {dificultadNueva} ({motivo})");
        EnviarPeticion(RegistrarAjusteCoroutine(datos));
    }

    IEnumerator RegistrarAjusteCoroutine(RegistrarAjusteRequest datos)
    {
        string json = JsonUtility.ToJson(datos);
        
        using (UnityWebRequest request = new UnityWebRequest(urlServidor + "/ajustes", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[MetricsClient] ✗ Error registrando ajuste: {request.error}");
            }
        }
    }

    // ============== CONFIGURACIÓN ==============

    /// <summary>
    /// Carga la configuración del servidor
    /// </summary>
    public void CargarConfiguracion()
    {
        if (!conectado) return;
        StartCoroutine(CargarConfiguracionCoroutine());
    }

    IEnumerator CargarConfiguracionCoroutine()
    {
        Debug.Log("[MetricsClient] Cargando configuración...");
        
        using (UnityWebRequest request = UnityWebRequest.Get(urlServidor + "/configuracion"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                configuracionActual = JsonUtility.FromJson<ConfiguracionResponse>(request.downloadHandler.text);
                Debug.Log("[MetricsClient] ✓ Configuración cargada");
                OnConfiguracionCargada?.Invoke(configuracionActual);
                AplicarConfiguracion();
            }
            else
            {
                Debug.LogWarning($"[MetricsClient] ✗ Error cargando configuración: {request.error}");
            }
        }
    }

    void AplicarConfiguracion()
    {
        if (configuracionActual == null) return;
        
        var dm = GameManager.Instance?.difficultyManager;
        if (dm == null) return;

        dm.configBaja.cantidadSenales = configuracionActual.senales_dificultad_baja;
        dm.configBaja.tiempoSegundos = configuracionActual.tiempo_dificultad_baja;
        dm.configBaja.mostrarAyudaVisual = configuracionActual.mostrar_ayuda_visual_baja;

        dm.configMedia.cantidadSenales = configuracionActual.senales_dificultad_media;
        dm.configMedia.tiempoSegundos = configuracionActual.tiempo_dificultad_media;
        dm.configMedia.incluirDistractores = configuracionActual.incluir_distractores_media;

        dm.configAlta.cantidadSenales = configuracionActual.senales_dificultad_alta;
        dm.configAlta.tiempoSegundos = configuracionActual.tiempo_dificultad_alta;
        dm.configAlta.incluirDistractores = configuracionActual.incluir_distractores_alta;



        if (GameManager.Instance != null)
        {
            GameManager.Instance.rondasPorZona = configuracionActual.rondas_por_zona;
            GameManager.Instance.tasaAciertosMinima = configuracionActual.tasa_aciertos_minima;
        }

        Debug.Log("[MetricsClient] ✓ Configuración aplicada al juego");
    }

    // ============== COLA DE PETICIONES ==============

    void EnviarPeticion(IEnumerator coroutine)
    {
        colaPeticiones.Enqueue(coroutine);
        
        if (!procesandoCola)
        {
            StartCoroutine(ProcesarCola());
        }
    }

    IEnumerator ProcesarCola()
    {
        procesandoCola = true;
        
        while (colaPeticiones.Count > 0)
        {
            yield return StartCoroutine(colaPeticiones.Dequeue());
        }
        
        procesandoCola = false;
    }

    // NUEVO: Método público para reintentar conexión
    public void ReintentarConexion()
    {
        if (!verificandoConexion)
        {
            StartCoroutine(VerificarConexion());
        }
    }
}
