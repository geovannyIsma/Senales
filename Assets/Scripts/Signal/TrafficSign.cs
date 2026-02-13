using UnityEngine;
using UnityEngine.Events;
 // Necesario para XR

// Forzamos que tenga el componente de interacción de XR
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))] 
public class TrafficSign : MonoBehaviour
{
    [Header("Datos en Tiempo Real")]
    public TrafficSignData datos; // Qu� tipo de se�al soy (Pare, Ceda...)
    public bool yaFueSeleccionada = false;

    [Header("Eventos")]
    public UnityEvent OnSeleccionada;

    // Referencia al material para cambiar el brillo (Opcional por ahora)
    private Renderer miRenderer;
    private SignalHighlightController highlightController;
    private float tiempoCreacion;
    private float tiempoInteraccion; // NUEVO: Cuando el jugador selecciona la señal
    
    // Referencia al componente XR
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable xrInteractable;

    void Awake()
    {
        // Intentamos buscar el renderer visual (puede estar en un hijo)
        miRenderer = GetComponentInChildren<Renderer>();
        highlightController = GetComponent<SignalHighlightController>();
        
        // Si no tiene highlight controller, agregarlo automáticamente
        if (highlightController == null)
        {
            highlightController = gameObject.AddComponent<SignalHighlightController>();
        }
        
        tiempoCreacion = Time.time;
        tiempoInteraccion = 0f; // NUEVO: No ha sido seleccionada aún
        
        ConfigurarInteraccionXR();
    }

    void ConfigurarInteraccionXR()
    {
        xrInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        // SI NO EXISTE, LO CREAMOS AUTOMÁTICAMENTE
        if (xrInteractable == null)
        {
            xrInteractable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        }
        
        // Configuracion basica del interactable (para que funcione bien con rayos)
        if (xrInteractable != null)
        {
            // Suscribirse a eventos XR
            xrInteractable.selectEntered.RemoveAllListeners();
            xrInteractable.hoverEntered.RemoveAllListeners();
            xrInteractable.hoverExited.RemoveAllListeners();

            // Conectar el Gatillo (Select) con Interactuar()
            xrInteractable.selectEntered.AddListener(args => Interactuar());

            // Conectar el Hover con el HighlightController
            xrInteractable.hoverEntered.AddListener(args => highlightController?.ActivarResaltado());
            xrInteractable.hoverExited.AddListener(args => highlightController?.DesactivarResaltado());
        }
    }

    // --- ESTA ES LA FUNCI�N QUE TE FALTA Y DABA EL ERROR ---
    public void ConfigurarSenal(TrafficSignData nuevosDatos)
    {
        datos = nuevosDatos;
        // Aqu� podr�as cambiar cosas visuales si quisieras
        // Por ejemplo: cambiar el nombre del objeto para encontrarlo f�cil en la jerarqu�a
        if (datos != null) gameObject.name = "Senal_" + datos.nombreSenal;
    }

    // Esta funci�n la llamar� tu mano o rayo VR
    public void Interactuar()
    {
        Debug.Log($"=== TrafficSign.Interactuar() llamado ===");
        Debug.Log($"yaFueSeleccionada: {yaFueSeleccionada}");
        Debug.Log($"GameState actual: {GameManager.Instance?.EstadoActual}");
        
        if (yaFueSeleccionada)
        {
            Debug.Log("Señal ya fue seleccionada, ignorando");
            return;
        }
        
        if (GameManager.Instance?.EstadoActual != GameState.RoundActive)
        {
            Debug.Log($"Estado no es RoundActive, es: {GameManager.Instance?.EstadoActual}");
            return;
        }

        // NUEVO: Registrar el momento de interacción
        tiempoInteraccion = Time.time;
        Debug.Log($"Tiempo de interacción registrado: {tiempoInteraccion}");

        // NUEVO: Reproducir sonido de selección
        GameAudioManager.Instance?.ReproducirSeleccion();

        // CAMBIO: Iniciar el temporizador de cuenta regresiva AHORA
        GameManager.Instance?.IniciarDesafioSenal();
        
        Debug.Log($"¡Jugador seleccionó señal: {datos?.nombreSenal}!");

        // Feedback visual de selección
        highlightController?.MostrarSeleccion();

        // Verificar si RecognitionMenuUI existe
        if (RecognitionMenuUI.Instance == null)
        {
            Debug.LogError("RecognitionMenuUI.Instance es NULL - ¿Existe en la escena?");
            return;
        }

        Debug.Log("Llamando a RecognitionMenuUI.Instance.MostrarMenu()");
        
        // Mostrar menú de reconocimiento
        RecognitionMenuUI.Instance.MostrarMenu(this);

        OnSeleccionada?.Invoke();
    }

    /// <summary>
    /// Llamado después de que el jugador responde en el menú
    /// </summary>
    public void MarcarComoIdentificada(bool fueCorrecta)
    {
        yaFueSeleccionada = true;
        
        // Mostrar resultado visual
        highlightController?.MostrarResultado(fueCorrecta);

        // Desaparecer la señal después de un breve tiempo para ver el feedback (luz verde/roja)
        Invoke(nameof(OcultarSenal), 0.9f);
    }

    void OcultarSenal()
    {
        gameObject.SetActive(false);    
    }

    /// <summary>
    /// Obtiene el tiempo desde que se creó la señal (tiempo de búsqueda + respuesta)
    /// </summary>
    public float ObtenerTiempoDesdeCreacion()
    {
        return Time.time - tiempoCreacion;
    }

    /// <summary>
    /// NUEVO: Obtiene el tiempo desde que el jugador interactuó con la señal (tiempo de reconocimiento puro)
    /// </summary>
    public float ObtenerTiempoDesdeInteraccion()
    {
        if (tiempoInteraccion <= 0)
        {
            // Si nunca interactuó, devolver tiempo desde creación
            return ObtenerTiempoDesdeCreacion();
        }
        return Time.time - tiempoInteraccion;
    }

    /// <summary>
    /// NUEVO: Obtiene el tiempo que tardó en encontrar la señal (desde spawn hasta selección)
    /// </summary>
    public float ObtenerTiempoBusqueda()
    {
        if (tiempoInteraccion <= 0)
        {
            return 0f; // Aún no ha sido seleccionada
        }
        return tiempoInteraccion - tiempoCreacion;
    }

    // Para sistemas XR Interaction Toolkit (Legacy o SendMessage)
    // Mantenemos esto por compatibilidad, aunque el Listener en Awake es más directo
    public void OnSelectEntered()
    {
        Interactuar();
    }
    
    // Para sistemas de raycast manual
    public void OnPointerClick()
    {
        Interactuar();
    }
}