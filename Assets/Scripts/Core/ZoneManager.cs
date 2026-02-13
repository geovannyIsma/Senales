using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ZonaData
{
    public string nombreZona;
    public string descripcion;
    public ZoneController controlador;
    public NivelDificultad dificultadMinima = NivelDificultad.Baja;
    public NivelDificultad dificultadMaxima = NivelDificultad.Media;
}

public class ZoneManager : MonoBehaviour
{
    [Header("Configuración de Zonas")]
    public List<ZonaData> zonas;
    
    [Header("Estado")]
    [SerializeField] private int zonaActual = -1;
    public int ZonaActual => zonaActual;
    public int TotalZonas => zonas.Count;

    public ZonaData ZonaActualData => zonaActual >= 0 && zonaActual < zonas.Count ? zonas[zonaActual] : null;

    void Awake()
    {
        // Desactivar todas las zonas al inicio
        foreach (var zona in zonas)
        {
            if (zona.controlador != null)
                zona.controlador.DesactivarZona();
        }
    }

    public void ActivarZona(int indice)
    {
        if (indice < 0 || indice >= zonas.Count)
        {
            Debug.LogError($"Índice de zona inválido: {indice}");
            return;
        }

        // Desactivar zona anterior
        if (zonaActual >= 0 && zonaActual < zonas.Count)
        {
            zonas[zonaActual].controlador?.DesactivarZona();
        }

        // Activar nueva zona
        zonaActual = indice;
        zonas[zonaActual].controlador?.ActivarZona();
        
        Debug.Log($"Zona activada: {zonas[zonaActual].nombreZona}");
    }

    public SignalSpawner ObtenerSpawnerZonaActual()
    {
        if (ZonaActualData?.controlador != null)
        {
            return ZonaActualData.controlador.GetComponent<SignalSpawner>();
        }
        return null;
    }

    public List<TrafficSignData> ObtenerSenalesDisponibles()
    {
        return ZonaActualData?.controlador?.senalesDisponibles ?? new List<TrafficSignData>();
    }
}
