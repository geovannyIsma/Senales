using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Panel de UI en VR para mostrar retroalimentación pedagógica generada por IA
/// </summary>
public class FeedbackPanelUI : MonoBehaviour
{
    public static FeedbackPanelUI Instance { get; private set; }

    [Header("Panel Principal")]
    public GameObject panelFeedback;
    public CanvasGroup canvasGroup;

    [Header("Contenido")]
    public TextMeshProUGUI textoTitulo;
    public TextMeshProUGUI textoSignificado;
    public TextMeshProUGUI textoMotivoError;
    public TextMeshProUGUI textoEjemploReal;
    public TextMeshProUGUI textoMnemotecnia;
    public Image imagenSenal;

    [Header("Botones")]
    public Button botonEntendido;
    public TextMeshProUGUI textoBotonEntendido;

    [Header("Indicador de Carga")]
    public GameObject indicadorCarga;
    public TextMeshProUGUI textoCargando;
    public LoadingSpinner spinnerAnimado; // NUEVO: referencia opcional al spinner

    [Header("Posicionamiento VR")]
    public Transform referenciaJugador;
    public float distanciaDelJugador = 2.5f;
    public float alturaOffset = 1.0f; // CAMBIO: Aumentado de 0.3f a 1.0f para que esté a nivel de ojos

    [Header("Animación")]
    public float duracionFadeIn = 0.3f;
    public float duracionFadeOut = 0.2f;
    public float tiempoAutoOcultar = 0f; // 0 = manual

    [Header("Audio Feedback")]
    public AudioSource audioSource;
    public AudioClip sonidoMostrar;
    public AudioClip sonidoOcultar;
    public bool usarAudioSourceGlobal = true; // NUEVO: Para evitar que se corte el sonido

    [Header("Estado")]
    [SerializeField] private bool estaVisible = false;
    [SerializeField] private bool esperandoRespuesta = false;

    private FeedbackResponse feedbackActual;
    private ErrorData errorActual;
    private Coroutine corutinaAutoOcultar;

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
        if (referenciaJugador == null)
        {
            referenciaJugador = Camera.main?.transform;
            if (referenciaJugador == null)
            {
                Debug.LogWarning("FeedbackPanelUI: No se encontró referencia del jugador");
                return;
            }
        }

        // Configurar botón
        if (botonEntendido != null)
        {
            botonEntendido.onClick.AddListener(OnBotonEntendido);
        }

        // Suscribirse a eventos del ErrorTracker
        var errorTracker = FindFirstObjectByType<ErrorTracker>();
        if (errorTracker != null)
        {
            errorTracker.OnErrorDetectado.AddListener(OnErrorDetectado);
            errorTracker.OnFeedbackListo.AddListener(OnFeedbackRecibido);
        }

        OcultarPanel(false);
    }

    void OnErrorDetectado(ErrorData error)
    {
        errorActual = error;
        MostrarCargando(error.nombreSenal);
    }

    void OnFeedbackRecibido(FeedbackResponse feedback)
    {
        feedbackActual = feedback;
        MostrarFeedback(feedback);
    }

    /// <summary>
    /// Muestra indicador de carga mientras se espera la respuesta de la IA
    /// </summary>
    public void MostrarCargando(string nombreSenal)
    {
        esperandoRespuesta = true;
        PosicionarFrenteAlJugador();

        // Activar spinner
        if (indicadorCarga != null) indicadorCarga.SetActive(true);
        if (textoCargando != null) textoCargando.text = $"Analizando '{nombreSenal}'";
        
        // Si tienes el componente spinner, actualizar el texto base
        if (spinnerAnimado != null)
        {
            spinnerAnimado.textoBase = $"Analizando '{nombreSenal}'";
        }

        // Ocultar contenido mientras carga
        OcultarContenido();

        panelFeedback?.SetActive(true);
        estaVisible = true;
        StartCoroutine(FadeIn());
    }

    /// <summary>
    /// Muestra la retroalimentación generada por la IA
    /// </summary>
    public void MostrarFeedback(FeedbackResponse feedback)
    {
        esperandoRespuesta = false;
        feedbackActual = feedback;

        // Ocultar carga
        if (indicadorCarga != null) indicadorCarga.SetActive(false);

        // Posicionar frente al jugador
        PosicionarFrenteAlJugador();

        // Configurar contenido
        ConfigurarContenido(feedback);

        // Mostrar panel
        panelFeedback?.SetActive(true);
        estaVisible = true;

        StartCoroutine(FadeIn());

        // Reproducir sonido
        if (audioSource != null && sonidoMostrar != null)
        {
            audioSource.PlayOneShot(sonidoMostrar);
        }

        // Auto-ocultar si está configurado
        if (tiempoAutoOcultar > 0)
        {
            if (corutinaAutoOcultar != null) StopCoroutine(corutinaAutoOcultar);
            corutinaAutoOcultar = StartCoroutine(AutoOcultar());
        }

        Debug.Log("FeedbackPanelUI: Mostrando retroalimentación");
    }

    void ConfigurarContenido(FeedbackResponse feedback)
    {
        // Título simple
        if (textoTitulo != null)
        {
            string nombreSenal = errorActual?.nombreSenal ?? "Señal";
            textoTitulo.text = $"<color=#FF6B6B>✗</color> Respuesta Incorrecta: {nombreSenal}";
        }

        // Significado de la señal
        if (textoSignificado != null)
        {
            textoSignificado.text = $"<b>📚 Significado:</b>\n{feedback.significado}";
        }

        // Motivo del error
        if (textoMotivoError != null)
        {
            textoMotivoError.text = $"<b>❓ ¿Qué pasó?</b>\n{feedback.motivo_error}";
        }

        // Ejemplo real
        if (textoEjemploReal != null)
        {
            textoEjemploReal.text = $"<b>🚗 Ejemplo real:</b>\n{feedback.ejemplo_real}";
        }

        // Mnemotecnia
        if (textoMnemotecnia != null)
        {
            textoMnemotecnia.text = $"<b>💡 Recuerda:</b>\n<i>\"{feedback.mnemotecnia}\"</i>";
        }

        // Imagen de la señal (si está disponible)
        if (imagenSenal != null && errorActual != null)
        {
            var senalesDisponibles = GameManager.Instance?.zoneManager?.ObtenerSenalesDisponibles();
            if (senalesDisponibles != null)
            {
                foreach (var senal in senalesDisponibles)
                {
                    if (senal.nombreSenal == errorActual.nombreSenal && senal.spriteSenal != null)
                    {
                        imagenSenal.sprite = senal.spriteSenal;
                        imagenSenal.gameObject.SetActive(true);
                        break;
                    }
                }
            }
        }

        // Texto del botón
        if (textoBotonEntendido != null)
        {
            textoBotonEntendido.text = feedback.success ? "¡Entendido!" : "Continuar";
        }
    }

    void OcultarContenido()
    {
        if (textoSignificado != null) textoSignificado.gameObject.SetActive(false);
        if (textoMotivoError != null) textoMotivoError.gameObject.SetActive(false);
        if (textoEjemploReal != null) textoEjemploReal.gameObject.SetActive(false);
        if (textoMnemotecnia != null) textoMnemotecnia.gameObject.SetActive(false);
        if (imagenSenal != null) imagenSenal.gameObject.SetActive(false);
        if (botonEntendido != null) botonEntendido.gameObject.SetActive(false);
    }

    void MostrarContenido()
    {
        if (textoSignificado != null) textoSignificado.gameObject.SetActive(true);
        if (textoMotivoError != null) textoMotivoError.gameObject.SetActive(true);
        if (textoEjemploReal != null) textoEjemploReal.gameObject.SetActive(true);
        if (textoMnemotecnia != null) textoMnemotecnia.gameObject.SetActive(true);
        if (botonEntendido != null) botonEntendido.gameObject.SetActive(true);
    }

    void PosicionarFrenteAlJugador()
    {
        if (referenciaJugador == null)
        {
            referenciaJugador = Camera.main?.transform;
            if (referenciaJugador == null)
            {
                Debug.LogWarning("FeedbackPanelUI: No se encontró referencia del jugador");
                return;
            }
        }

        // CAMBIO: Usar la dirección horizontal de la cámara (ignorando pitch/inclinación vertical)
        Vector3 direccion = referenciaJugador.forward;
        direccion.y = 0; // Forzar horizontal
        
        // Si el jugador mira directamente arriba/abajo, usar forward del mundo
        if (direccion.sqrMagnitude < 0.01f) 
        {
            direccion = Vector3.forward;
        }
        direccion.Normalize();

        // Calcular posición: frente al jugador a la distancia configurada
        Vector3 nuevaPosicion = referenciaJugador.position + direccion * distanciaDelJugador;
        
        // CAMBIO: La altura es relativa a los ojos del jugador (cámara), no al piso
        nuevaPosicion.y = referenciaJugador.position.y + alturaOffset;

        transform.position = nuevaPosicion;
        
        // CAMBIO: Mirar hacia el jugador correctamente
        Vector3 lookDirection = referenciaJugador.position - transform.position;
        lookDirection.y = 0; // Mantener el panel vertical (no inclinado)
        
        if (lookDirection.sqrMagnitude > 0.01f)
        {
            // Rotar para mirar AL jugador (el texto debe verse de frente)
            transform.rotation = Quaternion.LookRotation(-lookDirection);
        }
        
        Debug.Log($"FeedbackPanel posicionado en: {nuevaPosicion}, mirando hacia: {referenciaJugador.position}");
    }

    void OnBotonEntendido()
    {
        OcultarPanel(true);
        Debug.Log("FeedbackPanelUI: Usuario cerró el panel de retroalimentación");
    }

    public void OcultarPanel(bool conAnimacion = true)
    {
        if (conAnimacion)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            panelFeedback?.SetActive(false);
            estaVisible = false;
        }

        // CAMBIO: Removido - ahora se reproduce en OnBotonEntendido
        // El sonido ya se reprodujo antes de llamar a este método
    }

    IEnumerator FadeIn()
    {
        if (canvasGroup == null) 
        {
            MostrarContenido();
            yield break;
        }

        canvasGroup.alpha = 0;
        float tiempo = 0;

        while (tiempo < duracionFadeIn)
        {
            tiempo += Time.deltaTime;
            canvasGroup.alpha = tiempo / duracionFadeIn;
            yield return null;
        }

        canvasGroup.alpha = 1;
        MostrarContenido();
    }

    IEnumerator FadeOut()
    {
        if (canvasGroup == null)
        {
            panelFeedback?.SetActive(false);
            estaVisible = false;
            yield break;
        }

        float tiempo = 0;
        while (tiempo < duracionFadeOut)
        {
            tiempo += Time.deltaTime;
            canvasGroup.alpha = 1 - (tiempo / duracionFadeOut);
            yield return null;
        }

        canvasGroup.alpha = 0;
        panelFeedback?.SetActive(false);
        estaVisible = false;
    }

    IEnumerator AutoOcultar()
    {
        yield return new WaitForSeconds(tiempoAutoOcultar);
        OcultarPanel(true);
    }

    public bool EstaVisible => estaVisible;
    public bool EstaEsperando => esperandoRespuesta;
}
