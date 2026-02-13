using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SpawnPointsDeSenal
{
    public TrafficSignData senal;
    public List<Transform> puntosSpawn;
}

public class SignalSpawner : MonoBehaviour
{
    [Header("Puntos de Spawn por Señal")]
    public List<SpawnPointsDeSenal> spawnPointsPorSenal;

    private List<GameObject> senalesActivas = new List<GameObject>();
    private List<TrafficSign> referenciasSenales = new List<TrafficSign>();

    // Diccionario para acceso rápido
    private Dictionary<TrafficSignData, List<Transform>> mapaPuntos;

    void Awake()
    {
        ConstruirMapaPuntos();
    }

    void ConstruirMapaPuntos()
    {
        mapaPuntos = new Dictionary<TrafficSignData, List<Transform>>();
        foreach (var config in spawnPointsPorSenal)
        {
            if (config.senal != null && config.puntosSpawn != null)
            {
                mapaPuntos[config.senal] = new List<Transform>(config.puntosSpawn);
            }
        }
    }

    public void GenerarSenalesParaRonda(TrafficSignData senalObjetivo, List<TrafficSignData> senalesAGenerar)
    {
        LimpiarSenalesAnteriores();

        if (senalesAGenerar == null || senalesAGenerar.Count == 0) return;

        // Crear diccionario temporal de puntos disponibles
        Dictionary<TrafficSignData, List<Transform>> puntosDisponibles = new Dictionary<TrafficSignData, List<Transform>>();
        foreach (var kvp in mapaPuntos)
        {
            puntosDisponibles[kvp.Key] = new List<Transform>(kvp.Value);
        }

        // Spawnear cada señal de la lista
        foreach (var senalData in senalesAGenerar)
        {
            if (puntosDisponibles.ContainsKey(senalData) && puntosDisponibles[senalData].Count > 0)
            {
                SpawnearSenalEnSuPunto(senalData, puntosDisponibles);
            }
            else
            {
                Debug.LogWarning($"No hay puntos de spawn disponibles para: {senalData.nombreSenal}");
            }
        }

        string objetivoInfo = senalObjetivo != null ? senalObjetivo.nombreSenal : "TODAS";
        Debug.Log($"Spawneadas {senalesActivas.Count} señales. Objetivo: {objetivoInfo}");
    }

    /// <summary>
    /// Método legacy para compatibilidad (marca como obsoleto)
    /// </summary>
    [System.Obsolete("Usar GenerarSenalesParaRonda(TrafficSignData, List<TrafficSignData>) en su lugar")]
    public void GenerarSenalesParaRonda(TrafficSignData senalObjetivo, int cantidadTotal, List<TrafficSignData> todasLasSenales)
    {
        // Crear lista simple sin reglas de repetición (comportamiento legacy)
        List<TrafficSignData> senales = new List<TrafficSignData> { senalObjetivo };
        for (int i = 1; i < cantidadTotal; i++)
        {
            senales.Add(todasLasSenales[Random.Range(0, todasLasSenales.Count)]);
        }
        GenerarSenalesParaRonda(senalObjetivo, senales);
    }

    void SpawnearSenalEnSuPunto(TrafficSignData datos, Dictionary<TrafficSignData, List<Transform>> puntosDisponibles)
    {
        if (datos?.prefabSenal == null) return;
        if (!puntosDisponibles.ContainsKey(datos) || puntosDisponibles[datos].Count == 0) return;

        // Elegir punto al azar de los puntos específicos de esta señal
        List<Transform> puntosDeLaSenal = puntosDisponibles[datos];
        int indice = Random.Range(0, puntosDeLaSenal.Count);
        Transform punto = puntosDeLaSenal[indice];
        puntosDeLaSenal.RemoveAt(indice);

        // Crear señal
        GameObject nuevaSenal = Instantiate(datos.prefabSenal, punto.position, punto.rotation);
        
        // Configurar componente TrafficSign
        TrafficSign script = nuevaSenal.GetComponent<TrafficSign>();
        if (script == null)
        {
            script = nuevaSenal.AddComponent<TrafficSign>();
        }
        script.ConfigurarSenal(datos);

        // Agregar highlight controller si no existe
        if (nuevaSenal.GetComponent<SignalHighlightController>() == null)
        {
            nuevaSenal.AddComponent<SignalHighlightController>();
        }

        senalesActivas.Add(nuevaSenal);
        referenciasSenales.Add(script);
    }

    public List<TrafficSign> ObtenerSenalesActivas()
    {
        return new List<TrafficSign>(referenciasSenales);
    }

    public void LimpiarSenalesAnteriores()
    {
        foreach (GameObject senal in senalesActivas)
        {
            if (senal != null) Destroy(senal);
        }
        senalesActivas.Clear();
        referenciasSenales.Clear();
    }

    /// <summary>
    /// Obtiene el total de puntos de spawn disponibles para una señal específica
    /// </summary>
    public int ObtenerCantidadPuntosDeSenal(TrafficSignData senal)
    {
        if (mapaPuntos != null && mapaPuntos.ContainsKey(senal))
        {
            return mapaPuntos[senal].Count;
        }
        return 0;
    }

    /// <summary>
    /// Obtiene el total de puntos de spawn en toda la zona
    /// </summary>
    public int ObtenerTotalPuntosSpawn()
    {
        int total = 0;
        if (mapaPuntos != null)
        {
            foreach (var kvp in mapaPuntos)
            {
                total += kvp.Value.Count;
            }
        }
        return total;
    }
}