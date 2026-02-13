using UnityEngine;

/// <summary>
/// Configura automáticamente un Canvas para funcionar en VR
/// </summary>
[RequireComponent(typeof(Canvas))]
public class VRCanvasSetup : MonoBehaviour
{
    [Header("Configuración")]
    public bool configurarAutomaticamente = true;
    public float escalaCanvas = 0.001f; // Escala típica para VR (1px = 1mm)
    public float distanciaInicial = 2f;
    
    [Header("Seguimiento")]
    public bool seguirJugador = false;
    public float velocidadSeguimiento = 2f;
    public float distanciaSeguimiento = 2.5f;
    public float alturaSeguimiento = 0f;

    private Canvas canvas;
    private Transform jugador;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        
        if (configurarAutomaticamente)
        {
            ConfigurarParaVR();
        }
    }

    void Start()
    {
        jugador = Camera.main?.transform;
        
        if (jugador != null && configurarAutomaticamente)
        {
            PosicionarInicialmente();
        }
    }

    void LateUpdate()
    {
        if (seguirJugador && jugador != null)
        {
            SeguirAlJugador();
        }
    }

    void ConfigurarParaVR()
    {
        if (canvas == null) return;

        // Cambiar a World Space
        canvas.renderMode = RenderMode.WorldSpace;
        
        // Ajustar escala SIEMPRE, incluso si ya estaba en WorldSpace
        transform.localScale = Vector3.one * escalaCanvas;
        
        // Configurar tamaño del RectTransform
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(800, 600); // Tamaño en píxeles
        }

        Debug.Log($"Canvas '{gameObject.name}' configurado para VR con escala {escalaCanvas}");
    }

    void PosicionarInicialmente()
    {
        if (jugador == null) return;

        Vector3 direccion = jugador.forward;
        direccion.y = 0;
        direccion.Normalize();

        transform.position = jugador.position + direccion * distanciaInicial;
        transform.position += Vector3.up * alturaSeguimiento;
        
        // Mirar al jugador
        transform.LookAt(jugador.position);
        transform.Rotate(0, 180, 0);
    }

    void SeguirAlJugador()
    {
        // Calcular posición objetivo
        Vector3 direccion = jugador.forward;
        direccion.y = 0;
        direccion.Normalize();

        Vector3 posicionObjetivo = jugador.position + direccion * distanciaSeguimiento;
        posicionObjetivo.y = jugador.position.y + alturaSeguimiento;

        // Suavizar movimiento
        transform.position = Vector3.Lerp(
            transform.position,
            posicionObjetivo,
            Time.deltaTime * velocidadSeguimiento
        );

        // Suavizar rotación para mirar al jugador
        Vector3 lookDir = jugador.position - transform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-lookDir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * velocidadSeguimiento
            );
        }
    }

    /// <summary>
    /// Fuerza reposicionamiento frente al jugador
    /// </summary>
    public void ReposicionarFrenteAlJugador()
    {
        if (jugador == null)
        {
            jugador = Camera.main?.transform;
        }
        PosicionarInicialmente();
    }
}
