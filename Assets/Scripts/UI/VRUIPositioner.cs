using UnityEngine;

/// <summary>
/// Componente reutilizable para posicionar elementos de UI frente al jugador en VR/PC.
/// Extraído de RecognitionMenuUI para mejorar cohesión y reutilización.
/// </summary>
public class VRUIPositioner : MonoBehaviour
{
    [Header("Configuración de Posicionamiento")]
    public Transform referenciaJugador; // Asignar cámara o XR Origin
    public float distanciaDelJugador = 2f;
    public float alturaOffset = 0f;
    public bool seguirMirada = false; // Si true, actualizará posición en Update

    // Referencia al Canvas para ajustes específicos
    private Canvas miCanvas;
    private VRCanvasSetup vrSetup;

    void Awake()
    {
        miCanvas = GetComponentInParent<Canvas>();
        if (miCanvas != null)
        {
            vrSetup = miCanvas.GetComponent<VRCanvasSetup>();
        }
    }

    void Start()
    {
        if (referenciaJugador == null)
        {
            referenciaJugador = Camera.main?.transform;
        }
    }

    void Update()
    {
        if (seguirMirada && referenciaJugador != null)
        {
            PosicionarFrenteAlJugador();
        }
    }

    public void PosicionarFrenteAlJugador()
    {
        if (referenciaJugador == null)
        {
            referenciaJugador = Camera.main?.transform;
            if (referenciaJugador == null) return;
        }

        // Lógica de "congelamiento" si hay configuraciones conflictivas
        if (DebeMantenerPosicionActual()) return;

        // Calcular posición
        Vector3 direccion = referenciaJugador.forward;
        direccion.y = 0;
        if (direccion.sqrMagnitude < 0.001f) direccion = Vector3.forward;
        direccion.Normalize();

        Vector3 nuevaPosicion = referenciaJugador.position + direccion * distanciaDelJugador;
        nuevaPosicion.y = referenciaJugador.position.y + alturaOffset;

        // Aplicar transformación
        AplicarTransformacion(nuevaPosicion, referenciaJugador.position);
    }

    private bool DebeMantenerPosicionActual()
    {
        if (miCanvas != null)
        {
            // Ajuste de escala para WorldSpace (mantenido del código original)
            if (miCanvas.renderMode == RenderMode.WorldSpace && miCanvas.transform.localScale.x > 0.01f)
            {
                miCanvas.transform.localScale = Vector3.one * 0.0015f;
            }

            // Si es hijo de la cámara, desvincular
            if (miCanvas.transform.parent != null && 
                (miCanvas.transform.parent == referenciaJugador || miCanvas.transform.parent.GetComponent<Camera>() != null))
            {
                miCanvas.transform.SetParent(null);
                return true; // Ya estaba mal posicionado, lo soltamos pero no lo movemos este frame
            }

            // Si tiene VRCanvasSetup siguiéndolo
            if (vrSetup != null && vrSetup.seguirJugador)
            {
                vrSetup.seguirJugador = false; // Detener el seguimiento continuo del otro script
                return true; 
            }
        }
        return false;
    }

    private void AplicarTransformacion(Vector3 posicion, Vector3 mirarA)
    {
        Transform target = miCanvas != null && miCanvas.renderMode == RenderMode.WorldSpace ? 
                          miCanvas.transform : transform;

        target.position = posicion;
        target.LookAt(mirarA);
        target.Rotate(0, 180, 0); // Corregir inversión típica de UI 
    }
}
