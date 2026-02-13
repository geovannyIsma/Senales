using UnityEngine;

/// <summary>
/// Hace girar un elemento UI para indicar carga
/// </summary>
public class LoadingSpinner : MonoBehaviour
{
    [Header("Configuración")]
    public float velocidadRotacion = 200f; // Grados por segundo
    public bool girarEnSentidoHorario = true;

    [Header("Animación de Puntos (Opcional)")]
    public TMPro.TextMeshProUGUI textoCargando;
    public string textoBase = "Analizando";
    public float intervaloPuntos = 0.4f;

    private RectTransform rectTransform;
    private float tiempoPuntos;
    private int cantidadPuntos;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        // Reiniciar animación al activarse
        cantidadPuntos = 0;
        tiempoPuntos = 0;
    }

    void Update()
    {
        // Rotar el spinner
        if (rectTransform != null)
        {
            float direccion = girarEnSentidoHorario ? -1f : 1f;
            rectTransform.Rotate(0, 0, velocidadRotacion * direccion * Time.deltaTime);
        }

        // Animar puntos suspensivos
        if (textoCargando != null)
        {
            tiempoPuntos += Time.deltaTime;
            if (tiempoPuntos >= intervaloPuntos)
            {
                tiempoPuntos = 0;
                cantidadPuntos = (cantidadPuntos + 1) % 4; // 0, 1, 2, 3, 0, 1...
                textoCargando.text = textoBase + new string('.', cantidadPuntos);
            }
        }
    }
}
