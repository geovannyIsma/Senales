using UnityEngine;

[CreateAssetMenu(fileName = "NuevaSenal", menuName = "Trafico/Datos De Senal")]
public class TrafficSignData : ScriptableObject
{
    [Header("Informacion Basica")]
    public string nombreSenal; // Ej: "Pare"
    public GameObject prefabSenal; // Arrastra aquí tu prefab (senal_pare)

    [Header("Visual para UI")]
    public Sprite spriteSenal; // Imagen para mostrar en menú de reconocimiento

    [Header("Dificultad")]
    public int nivelDificultad = 1;
}