using UnityEngine;

[CreateAssetMenu(fileName = "GameData", menuName = "Trafico/Datos del Juego")]
public class GameData : ScriptableObject
{
    [Header("Información del Juego")]
    public string nombreJuego = "LearnSignals XR";
    public string subtitulo = "Aprende señales de tránsito en realidad virtual";
    public string version = "1.0.0";
    
    [Header("Descripción")]
    [TextArea(3, 5)]
    public string descripcion = "Bienvenido a LearnSignals XR. Aprende a identificar señales de tránsito de forma interactiva en un entorno de realidad virtual.";
    
    [Header("Instrucciones")]
    [TextArea(3, 5)]
    public string instrucciones = "• Usa el control para apuntar a las señales\n• Presiona el gatillo para seleccionar\n• Identifica correctamente cada señal\n• ¡Mejora tu puntuación!";
    
    [Header("Créditos")]
    public string desarrollador = "Tu Nombre";
    public string universidad = "Tu Universidad";
}
