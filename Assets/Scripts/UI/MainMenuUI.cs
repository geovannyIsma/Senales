using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Controlador de la pantalla de inicio del juego LearnSignals XR
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance { get; private set; }

    [Header("Datos del Juego")]
    public GameData datosJuego;

    [Header("Panel Principal")]
    public GameObject panelMenuPrincipal;
    public CanvasGroup canvasGroup;
    public Canvas canvasMenu; // NUEVO: Referencia directa al Canvas

    [Header("Textos")]
    public TextMeshProUGUI textoTitulo;
    public TextMeshProUGUI textoSubtitulo;
    public TextMeshProUGUI textoVersion;
    public TextMeshProUGUI textoDescripcion;
    public TextMeshProUGUI textoInstrucciones;

    [Header("Botones")]
    public Button botonIniciar;
    public Button botonOpciones;
    public Button botonSalir;
    public TextMeshProUGUI textoBotonIniciar;

    [Header("Animación")]
    public float duracionFadeIn = 1f;
    public float retardoInicial = 0.5f;
    public bool animarEntrada = true;

    [Header("Posicionamiento VR")]
    public Transform referenciaJugador;
    public float distanciaDelJugador = 3f;
    public float alturaOffset = 0.3f; // Ajuste: Valor positivo sube el menú, negativo lo baja
    public float escalaCanvas = 0.001f; // NUEVO: Escala para VR

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip sonidoBoton;
    public AudioClip musicaMenu;
    public float delayBotonIniciar = 0.15f; // NUEVO: Delay para que suene el botón

    [Header("Debug")]
    public bool mostrarDebugGizmos = true;

    private bool menuActivo = false;

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
        // Buscar referencia del jugador
        if (referenciaJugador == null)
        {
            referenciaJugador = Camera.main?.transform;
        }

        // NUEVO: Buscar Canvas si no está asignado
        if (canvasMenu == null)
        {
            canvasMenu = GetComponentInParent<Canvas>();
            if (canvasMenu == null && panelMenuPrincipal != null)
            {
                canvasMenu = panelMenuPrincipal.GetComponentInParent<Canvas>();
            }
        }

        // NUEVO: Buscar CanvasGroup si no está asignado
        if (canvasGroup == null && panelMenuPrincipal != null)
        {
            canvasGroup = panelMenuPrincipal.GetComponent<CanvasGroup>();
        }

        // Configurar Canvas para VR
        ConfigurarCanvasParaVR();

        // Configurar datos del juego
        ConfigurarDatosUI();

        // Configurar botones
        ConfigurarBotones();

        // Iniciar música si existe
        if (audioSource != null && musicaMenu != null)
        {
            audioSource.clip = musicaMenu;
            audioSource.loop = true;
            audioSource.Play();
        }

        Debug.Log($"MainMenuUI Start - Panel: {panelMenuPrincipal?.name ?? "NULL"}, Canvas: {canvasMenu?.name ?? "NULL"}");
    }

    void ConfigurarCanvasParaVR()
    {
        if (canvasMenu == null)
        {
            Debug.LogError("MainMenuUI: No se encontró Canvas!");
            return;
        }

        // Configurar como WorldSpace para VR
        canvasMenu.renderMode = RenderMode.WorldSpace;
        
        // Ajustar escala para VR (muy importante!)
        canvasMenu.transform.localScale = Vector3.one * escalaCanvas;

        // Configurar RectTransform
        RectTransform rect = canvasMenu.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(1200, 800); // Tamaño en píxeles
        }

        Debug.Log($"MainMenuUI: Canvas configurado - RenderMode: {canvasMenu.renderMode}, Escala: {canvasMenu.transform.localScale}");
    }

    void ConfigurarDatosUI()
    {
        if (datosJuego == null)
        {
            Debug.LogWarning("MainMenuUI: No hay GameData asignado, usando valores por defecto");
            if (textoTitulo != null) textoTitulo.text = "LearnSignals XR";
            if (textoSubtitulo != null) textoSubtitulo.text = "Aprende señales de tránsito en VR";
            if (textoVersion != null) textoVersion.text = "v1.0.0";
            return;
        }

        if (textoTitulo != null) textoTitulo.text = datosJuego.nombreJuego;
        if (textoSubtitulo != null) textoSubtitulo.text = datosJuego.subtitulo;
        if (textoVersion != null) textoVersion.text = $"v{datosJuego.version}";
        if (textoDescripcion != null) textoDescripcion.text = datosJuego.descripcion;
        if (textoInstrucciones != null) textoInstrucciones.text = datosJuego.instrucciones;
    }

    void ConfigurarBotones()
    {
        if (botonIniciar != null)
        {
            botonIniciar.onClick.AddListener(OnBotonIniciar);
            if (textoBotonIniciar != null)
                textoBotonIniciar.text = "INICIAR";
        }

        if (botonOpciones != null)
        {
            botonOpciones.onClick.AddListener(OnBotonOpciones);
        }

        if (botonSalir != null)
        {
            botonSalir.onClick.AddListener(OnBotonSalir);
        }
    }

    public void MostrarMenu()
    {
        Debug.Log("=== MainMenuUI.MostrarMenu() llamado ===");
        
        menuActivo = true;
        
        // Activar panel
        if (panelMenuPrincipal != null)
        {
            panelMenuPrincipal.SetActive(true);
            Debug.Log($"Panel activado: {panelMenuPrincipal.activeSelf}");
        }
        else
        {
            Debug.LogError("MainMenuUI: panelMenuPrincipal es NULL!");
            return;
        }

        // IMPORTANTE: Asegurar que el CanvasGroup sea visible
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f; // Forzar visible
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            Debug.Log($"CanvasGroup - Alpha: {canvasGroup.alpha}, Interactable: {canvasGroup.interactable}");
        }

        // Posicionar frente al jugador
        PosicionarFrenteAlJugador();

        // Animar entrada (opcional)
        if (animarEntrada && canvasGroup != null)
        {
            StartCoroutine(AnimarEntrada());
        }

        // Forzar actualización del Canvas
        Canvas.ForceUpdateCanvases();

        Debug.Log($"MainMenuUI: Menú mostrado en posición {transform.position}");
    }

    public void OcultarMenu()
    {
        menuActivo = false;
        
        if (canvasGroup != null)
        {
            StartCoroutine(AnimarSalida());
        }
        else
        {
            panelMenuPrincipal?.SetActive(false);
        }

        // Detener música del menú
        if (audioSource != null && musicaMenu != null)
        {
            audioSource.Stop();
        }
    }

    void PosicionarFrenteAlJugador()
    {
        if (referenciaJugador == null)
        {
            referenciaJugador = Camera.main?.transform;
            if (referenciaJugador == null)
            {
                Debug.LogError("MainMenuUI: No se encontró referencia del jugador!");
                return;
            }
        }

        // Calcular dirección (solo horizontal)
        Vector3 direccion = referenciaJugador.forward;
        direccion.y = 0;
        if (direccion.sqrMagnitude < 0.01f) direccion = Vector3.forward;
        direccion.Normalize();

        // Calcular posición - CAMBIO: Usar la altura de la cámara directamente
        Vector3 nuevaPosicion = referenciaJugador.position + direccion * distanciaDelJugador;
        
        // CAMBIO: La altura es la misma que la cámara + un pequeño offset (puede ser negativo para bajar)
        nuevaPosicion.y = referenciaJugador.position.y + alturaOffset;

        // Aplicar al Canvas (no al panel)
        Transform targetTransform = canvasMenu != null ? canvasMenu.transform : transform;
        targetTransform.position = nuevaPosicion;

        // Mirar hacia el jugador
        Vector3 lookDir = referenciaJugador.position - targetTransform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            targetTransform.rotation = Quaternion.LookRotation(-lookDir);
        }

        Debug.Log($"MainMenuUI posicionado - Pos: {nuevaPosicion}, CamaraY: {referenciaJugador.position.y}, Offset: {alturaOffset}");
    }

    IEnumerator AnimarEntrada()
    {
        if (canvasGroup == null) yield break;
        
        canvasGroup.alpha = 0;
        yield return new WaitForSeconds(retardoInicial);

        float tiempo = 0;
        while (tiempo < duracionFadeIn)
        {
            tiempo += Time.deltaTime;
            canvasGroup.alpha = tiempo / duracionFadeIn;
            yield return null;
        }
        canvasGroup.alpha = 1;
    }

    IEnumerator AnimarSalida()
    {
        if (canvasGroup == null)
        {
            panelMenuPrincipal?.SetActive(false);
            yield break;
        }

        float tiempo = 0;
        float duracion = 0.3f;
        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            canvasGroup.alpha = 1 - (tiempo / duracion);
            yield return null;
        }
        canvasGroup.alpha = 0;
        panelMenuPrincipal?.SetActive(false);
    }

    void OnBotonIniciar()
    {
        ReproducirSonidoBoton();
        Debug.Log("MainMenuUI: Botón Iniciar presionado");
        
        // CAMBIO: Usar coroutine para dar tiempo al sonido
        StartCoroutine(IniciarConDelay());
    }

    // NUEVO: Coroutine para dar tiempo al sonido antes de cambiar de escena
    IEnumerator IniciarConDelay()
    {
        // Deshabilitar botón para evitar doble click
        if (botonIniciar != null)
            botonIniciar.interactable = false;
        
        // Esperar a que suene el botón
        yield return new WaitForSeconds(delayBotonIniciar);
        
        OcultarMenu();
        
        // Notificar al GameManager para comenzar el juego
        GameManager.Instance?.IniciarDesdeMenuPrincipal();
    }

    void OnBotonOpciones()
    {
        ReproducirSonidoBoton();
        Debug.Log("MainMenuUI: Botón Opciones presionado (No implementado)");
    }

    void OnBotonSalir()
    {
        ReproducirSonidoBoton();
        Debug.Log("MainMenuUI: Saliendo del juego");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    void ReproducirSonidoBoton()
    {
        if (audioSource != null && sonidoBoton != null)
        {
            audioSource.PlayOneShot(sonidoBoton);
        }
    }

    public bool EstaActivo() => menuActivo;

    // NUEVO: Debug visual
    void OnDrawGizmos()
    {
        if (!mostrarDebugGizmos) return;
        
        // Mostrar dónde aparecerá el menú
        Transform refJugador = referenciaJugador;
        if (refJugador == null && Camera.main != null)
        {
            refJugador = Camera.main.transform;
        }
        
        if (refJugador != null)
        {
            Vector3 direccion = refJugador.forward;
            direccion.y = 0;
            if (direccion.sqrMagnitude > 0.01f) direccion.Normalize();
            else direccion = Vector3.forward;

            Vector3 posMenu = refJugador.position + direccion * distanciaDelJugador;
            posMenu.y = refJugador.position.y + alturaOffset;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(posMenu, new Vector3(1.2f, 0.8f, 0.01f));
            Gizmos.DrawLine(refJugador.position, posMenu);
        }
    }
}
