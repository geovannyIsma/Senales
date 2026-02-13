using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class OpcionReconocimiento
{
    public TrafficSignData datos;
    public Button boton;
    public Image imagenSenal;
    public TextMeshProUGUI textoNombre;
}

public class RecognitionMenuUI : MonoBehaviour
{
    public static RecognitionMenuUI Instance { get; private set; }

    [Header("Panel Principal")]
    public GameObject panelMenu;
    public TextMeshProUGUI textoInstruccion;

    [Header("Contenedor de Opciones")]
    public Transform contenedorOpciones;
    public GameObject prefabOpcion;

    [Header("Posicionamiento VR")]
    public Transform referenciaJugador; // Asignar la cámara principal o XR Origin
    public float distanciaDelJugador = 2f;
    public float alturaOffset = 0f; // Ajuste de altura respecto a la vista
    public bool seguirMiradaAlAbrir = true;

    [Header("Opciones Generadas")]
    [SerializeField] private List<OpcionReconocimiento> opcionesActuales = new List<OpcionReconocimiento>();

    [Header("Configuración")]
    public int opcionesPorDificultad_Baja = 2;
    public int opcionesPorDificultad_Media = 3;
    public int opcionesPorDificultad_Alta = 4;

    [Header("Estado")]
    [SerializeField] private TrafficSign senalSeleccionada;
    public TrafficSign SenalSeleccionada => senalSeleccionada; // Hacemos público el getter para el GameManager

    [SerializeField] private bool menuActivo = false;

    [Header("Eventos")]
    public UnityEvent<TrafficSign, TrafficSignData, bool> OnRespuestaElegida; // (señal, respuesta, esCorrecta)

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
        // Buscar cámara principal si no está asignada
        if (referenciaJugador == null)
        {
            referenciaJugador = Camera.main?.transform;
        }
        
        // Validar referencias críticas
        ValidarReferencias();
        
        OcultarMenu();
    }

    void ValidarReferencias()
    {
        if (panelMenu == null)
        {
            Debug.LogError("RecognitionMenuUI: panelMenu NO está asignado en el Inspector!");
        }
        if (contenedorOpciones == null)
        {
            Debug.LogError("RecognitionMenuUI: contenedorOpciones NO está asignado en el Inspector!");
        }
        if (prefabOpcion == null)
        {
            Debug.LogError("RecognitionMenuUI: prefabOpcion NO está asignado en el Inspector!");
        }
    }

    /// <summary>
    /// Muestra el menú de reconocimiento para una señal seleccionada
    /// </summary>
    public void MostrarMenu(TrafficSign senal)
    {
        Debug.Log($"=== MostrarMenu llamado para señal: {senal?.datos?.nombreSenal ?? "NULL"} ===");
        
        if (senal == null)
        {
            Debug.LogError("MostrarMenu: La señal es NULL");
            return;
        }
        
        if (senal.datos == null)
        {
            Debug.LogError("MostrarMenu: Los datos de la señal son NULL");
            return;
        }

        if (panelMenu == null)
        {
            Debug.LogError("MostrarMenu: panelMenu es NULL - asignar en el Inspector!");
            return;
        }

        senalSeleccionada = senal;
        menuActivo = true;

        // NUEVO: Reproducir sonido al abrir menú
        GameAudioManager.Instance?.ReproducirAbrirMenu();

        // Posicionar menú frente al jugador (AQUÍ ESTÁ LA MAGIA PARA QUE NO SE MUEVA)
        PosicionarMenuFrenteAlJugador();

        // Obtener cantidad de opciones según dificultad
        int cantidadOpciones = ObtenerCantidadOpciones();
        Debug.Log($"Generando {cantidadOpciones} opciones para el menú");

        // Generar opciones
        GenerarOpciones(senal.datos, cantidadOpciones);

        // Mostrar instrucción
        if (textoInstruccion != null)
        {
            textoInstruccion.text = "¿Qué señal es esta?";
        }

        // Activar el panel
        panelMenu.SetActive(true);
        
        // Verificar que realmente se activó
        Debug.Log($"Panel activado: {panelMenu.activeSelf}, Posición: {panelMenu.transform.position}");
        
        // Forzar Canvas a actualizarse
        Canvas.ForceUpdateCanvases();
    }

    void PosicionarMenuFrenteAlJugador()
    {
        // Intentar usar el nuevo componente si existe
        var positioner = GetComponent<VRUIPositioner>();
        if (positioner == null && panelMenu != null)
        {
             positioner = panelMenu.GetComponent<VRUIPositioner>();
        }
        
        if (positioner != null)
        {
            positioner.PosicionarFrenteAlJugador();
            return;
        }

        // --- FALLBACK LOGIC (Código legado simplificado) ---
        if (referenciaJugador == null) 
        {
            referenciaJugador = Camera.main?.transform;
            if (referenciaJugador == null) return;
        }

        // Lógica básica si no hay componente dedicado
        if (panelMenu != null)
        {
             Vector3 direccion = referenciaJugador.forward;
             direccion.y = 0;
             direccion.Normalize();
             
             Vector3 nuevaPos = referenciaJugador.position + direccion * distanciaDelJugador;
             nuevaPos.y = referenciaJugador.position.y + alturaOffset;
             
             panelMenu.transform.position = nuevaPos;
             panelMenu.transform.LookAt(referenciaJugador.position);
             panelMenu.transform.Rotate(0, 180, 0);
        }
    }

    public void OcultarMenu()
    {
        // NUEVO: Reproducir sonido al cerrar menú (antes de desactivar)
        if (menuActivo)
        {
            GameAudioManager.Instance?.ReproducirCerrarMenu();
        }

        menuActivo = false;
        senalSeleccionada = null;
        panelMenu?.SetActive(false);

        LimpiarOpciones();

        // --- REACTIVAR SEGUIMIENTO ---
        // Al ocultar el menú, reactivamos el seguimiento para que la próxima vez
        // que lo muestres, ya esté "volando" cerca de tu cabeza invisiblemente.
        if (panelMenu != null)
        {
            Canvas canvas = panelMenu.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var vrSetup = canvas.GetComponent<VRCanvasSetup>();
                if (vrSetup != null)
                {
                    vrSetup.seguirJugador = true;
                }
            }
        }
    }

    int ObtenerCantidadOpciones()
    {
        var dificultad = GameManager.Instance?.difficultyManager?.DificultadActual ?? NivelDificultad.Baja;
        
        return dificultad switch
        {
            NivelDificultad.Baja => opcionesPorDificultad_Baja,
            NivelDificultad.Media => opcionesPorDificultad_Media,
            NivelDificultad.Alta => opcionesPorDificultad_Alta,
            _ => opcionesPorDificultad_Baja
        };
    }

    void GenerarOpciones(TrafficSignData respuestaCorrecta, int cantidad)
    {
        LimpiarOpciones();

        if (prefabOpcion == null)
        {
            Debug.LogError("GenerarOpciones: prefabOpcion es NULL!");
            return;
        }
        
        if (contenedorOpciones == null)
        {
            Debug.LogError("GenerarOpciones: contenedorOpciones es NULL!");
            return;
        }

        // Obtener señales disponibles para opciones incorrectas
        var senalesDisponibles = GameManager.Instance?.zoneManager?.ObtenerSenalesDisponibles() 
            ?? new List<TrafficSignData>();

        Debug.Log($"Señales disponibles para opciones: {senalesDisponibles.Count}");

        // Crear lista de opciones
        List<TrafficSignData> opciones = new List<TrafficSignData> { respuestaCorrecta };

        // Agregar opciones incorrectas
        List<TrafficSignData> incorrectas = new List<TrafficSignData>();
        foreach (var senal in senalesDisponibles)
        {
            if (senal != respuestaCorrecta)
            {
                incorrectas.Add(senal);
            }
        }

        ShuffleList(incorrectas);

        for (int i = 0; i < cantidad - 1 && i < incorrectas.Count; i++)
        {
            opciones.Add(incorrectas[i]);
        }

        ShuffleList(opciones);

        Debug.Log($"Creando {opciones.Count} botones de opción");

        // Crear botones de UI
        foreach (var opcionData in opciones)
        {
            CrearBotonOpcion(opcionData);
        }
        
        Debug.Log($"Opciones creadas: {opcionesActuales.Count}");
    }

    void CrearBotonOpcion(TrafficSignData datos)
    {
        if (prefabOpcion == null || contenedorOpciones == null) return;

        GameObject nuevoBoton = Instantiate(prefabOpcion, contenedorOpciones);
        
        // FIX: Resetear escala local para evitar botones gigantes
        nuevoBoton.transform.localScale = Vector3.one;

        OpcionReconocimiento opcion = new OpcionReconocimiento
        {
            datos = datos,
            boton = nuevoBoton.GetComponent<Button>(),
            imagenSenal = nuevoBoton.GetComponentInChildren<Image>(),
            textoNombre = nuevoBoton.GetComponentInChildren<TextMeshProUGUI>()
        };

        // Configurar imagen (usar sprite del ScriptableObject si existe)
        if (opcion.imagenSenal != null && datos.spriteSenal != null)
        {
            opcion.imagenSenal.sprite = datos.spriteSenal;
        }

        // Configurar texto
        if (opcion.textoNombre != null)
        {
            opcion.textoNombre.text = datos.nombreSenal;
        }

        // Configurar evento del botón
        if (opcion.boton != null)
        {
            TrafficSignData datosCapturados = datos; // Capturar para closure
            opcion.boton.onClick.AddListener(() => OnOpcionSeleccionada(datosCapturados));
        }

        opcionesActuales.Add(opcion);
    }

    void OnOpcionSeleccionada(TrafficSignData respuestaElegida)
    {
        if (senalSeleccionada == null) return;

        // CAMBIO: Detener el temporizador inmediatamente al seleccionar
        GameManager.Instance?.DetenerDesafioSenal();

        bool esCorrecta = respuestaElegida == senalSeleccionada.datos;

        Debug.Log($"Respuesta: {respuestaElegida.nombreSenal} - {(esCorrecta ? "¡Correcto!" : "Incorrecto")}");

        // Notificar resultado
        OnRespuestaElegida?.Invoke(senalSeleccionada, respuestaElegida, esCorrecta);

        // Procesar en el RoundManager
        GameManager.Instance?.roundManager?.ProcesarRespuestaReconocimiento(
            senalSeleccionada, 
            respuestaElegida, 
            esCorrecta
        );

        OcultarMenu();
    }

    void LimpiarOpciones()
    {
        foreach (var opcion in opcionesActuales)
        {
            if (opcion.boton != null)
            {
                Destroy(opcion.boton.gameObject);
            }
        }
        opcionesActuales.Clear();
    }

    void ShuffleList<T>(List<T> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = lista[i];
            lista[i] = lista[j];
            lista[j] = temp;
        }
    }

    public bool EstaActivo() => menuActivo;
}
