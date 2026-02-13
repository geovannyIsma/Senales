using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class RegistroIntento
{
    public string nombreSenal;
    public bool fueCorrecta;
    public float tiempoRespuesta;
    public NivelDificultad dificultad;
    public float timestamp;
    public int zonaIndex; // Nuevo: guardar en qué zona ocurrió
}

[System.Serializable]
public class MetricasRecientes
{
    public int intentosTotales;
    public int aciertos;
    public int errores; // Nuevo
    public float tasaAciertos;
    public float tiempoPromedioRespuesta;
    public NivelDificultad dificultadActual;
    public int zonaActual; // Nuevo
}

public class PerformanceTracker : MonoBehaviour
{
    [Header("Historial")]
    [SerializeField] private List<RegistroIntento> historialCompleto = new List<RegistroIntento>();
    [SerializeField] private List<RegistroIntento> intentosRecientes = new List<RegistroIntento>();

    [Header("Configuración")]
    public int ventanaReciente = 5; // Últimos N intentos para evaluar

    [Header("Métricas de Zona Actual")]
    [SerializeField] private int totalIntentosZona = 0;
    [SerializeField] private int totalAciertosZona = 0;
    [SerializeField] private float tiempoPromedioZona = 0f;

    // NUEVO: Métricas globales del juego completo (NO se reinician al cambiar de zona)
    [Header("Métricas Globales del Juego")]
    [SerializeField] private int totalIntentosJuego = 0;
    [SerializeField] private int totalAciertosJuego = 0;
    [SerializeField] private float sumaTiemposJuego = 0f;
    [SerializeField] private int intentosConTiempoJuego = 0;

    [Header("Eventos")]
    public UnityEvent<MetricasRecientes> OnMetricasActualizadas; // NUEVO: Evento para actualización en tiempo real

    public void RegistrarIntento(TrafficSignData senal, bool correcta, float tiempo, NivelDificultad dificultad)
    {
        int zonaActual = GameManager.Instance?.zoneManager?.ZonaActual ?? 0;
        
        var registro = new RegistroIntento
        {
            nombreSenal = senal?.nombreSenal ?? "Ninguna",
            fueCorrecta = correcta,
            tiempoRespuesta = tiempo,
            dificultad = dificultad,
            timestamp = Time.time,
            zonaIndex = zonaActual
        };

        // Agregar al historial
        historialCompleto.Add(registro);
        intentosRecientes.Add(registro);

        // Mantener ventana reciente
        while (intentosRecientes.Count > ventanaReciente)
        {
            intentosRecientes.RemoveAt(0);
        }

        // Actualizar métricas de ZONA
        totalIntentosZona++;
        if (correcta) totalAciertosZona++;
        
        // Calcular tiempo promedio de zona
        if (tiempo > 0)
        {
            int intentosConTiempoZona = 0;
            float sumaTiemposZona = 0f;
            foreach (var r in historialCompleto)
            {
                if (r.zonaIndex == zonaActual && r.tiempoRespuesta > 0)
                {
                    sumaTiemposZona += r.tiempoRespuesta;
                    intentosConTiempoZona++;
                }
            }
            tiempoPromedioZona = intentosConTiempoZona > 0 ? sumaTiemposZona / intentosConTiempoZona : 0f;
        }

        // NUEVO: Actualizar métricas GLOBALES del juego (nunca se reinician hasta nuevo juego)
        totalIntentosJuego++;
        if (correcta) totalAciertosJuego++;
        if (tiempo > 0)
        {
            sumaTiemposJuego += tiempo;
            intentosConTiempoJuego++;
        }

        Debug.Log($"Intento registrado: {registro.nombreSenal} - {(correcta ? "Correcto" : "Incorrecto")} - {tiempo:F2}s | Zona: {totalAciertosZona}/{totalIntentosZona} | Juego: {totalAciertosJuego}/{totalIntentosJuego}");

        // Notificar a los listeners
        OnMetricasActualizadas?.Invoke(ObtenerMetricasGlobales());
    }

    public MetricasRecientes ObtenerMetricasRecientes()
    {
        int aciertosRecientes = 0;
        float sumaTiempos = 0f;
        int conTiempo = 0;

        foreach (var intento in intentosRecientes)
        {
            if (intento.fueCorrecta) aciertosRecientes++;
            if (intento.tiempoRespuesta > 0)
            {
                sumaTiempos += intento.tiempoRespuesta;
                conTiempo++;
            }
        }

        int erroresRecientes = intentosRecientes.Count - aciertosRecientes;
        int zonaActual = GameManager.Instance?.zoneManager?.ZonaActual ?? 0;

        return new MetricasRecientes
        {
            intentosTotales = intentosRecientes.Count,
            aciertos = aciertosRecientes,
            errores = erroresRecientes,
            tasaAciertos = intentosRecientes.Count > 0 ? (float)aciertosRecientes / intentosRecientes.Count : 0f,
            tiempoPromedioRespuesta = conTiempo > 0 ? sumaTiempos / conTiempo : 0f,
            dificultadActual = intentosRecientes.Count > 0 ? intentosRecientes[^1].dificultad : NivelDificultad.Baja,
            zonaActual = zonaActual
        };
    }

    /// <summary>
    /// Obtiene métricas de la zona actual (se reinician al cambiar de zona)
    /// </summary>
    public MetricasRecientes ObtenerMetricasGlobales()
    {
        int erroresZona = totalIntentosZona - totalAciertosZona;
        int zonaActual = GameManager.Instance?.zoneManager?.ZonaActual ?? 0;
        
        return new MetricasRecientes
        {
            intentosTotales = totalIntentosZona,
            aciertos = totalAciertosZona,
            errores = erroresZona,
            tasaAciertos = totalIntentosZona > 0 ? (float)totalAciertosZona / totalIntentosZona : 0f,
            tiempoPromedioRespuesta = tiempoPromedioZona,
            dificultadActual = GameManager.Instance?.difficultyManager?.DificultadActual ?? NivelDificultad.Baja,
            zonaActual = zonaActual
        };
    }

    /// <summary>
    /// NUEVO: Obtiene métricas de TODO el juego (todas las zonas combinadas)
    /// Usado para el panel de fin de juego
    /// </summary>
    public MetricasRecientes ObtenerMetricasJuegoCompleto()
    {
        int erroresJuego = totalIntentosJuego - totalAciertosJuego;
        float tiempoPromedioJuego = intentosConTiempoJuego > 0 ? sumaTiemposJuego / intentosConTiempoJuego : 0f;
        
        return new MetricasRecientes
        {
            intentosTotales = totalIntentosJuego,
            aciertos = totalAciertosJuego,
            errores = erroresJuego,
            tasaAciertos = totalIntentosJuego > 0 ? (float)totalAciertosJuego / totalIntentosJuego : 0f,
            tiempoPromedioRespuesta = tiempoPromedioJuego,
            dificultadActual = GameManager.Instance?.difficultyManager?.DificultadActual ?? NivelDificultad.Baja,
            zonaActual = GameManager.Instance?.zoneManager?.ZonaActual ?? 0
        };
    }

    /// <summary>
    /// Calcula la precisión de la zona actual
    /// </summary>
    public float CalcularPrecisionGlobal()
    {
        if (totalIntentosZona <= 0) return 100f;
        return (float)totalAciertosZona / totalIntentosZona * 100f;
    }

    /// <summary>
    /// NUEVO: Calcula la precisión de todo el juego
    /// </summary>
    public float CalcularPrecisionJuegoCompleto()
    {
        if (totalIntentosJuego <= 0) return 100f;
        return (float)totalAciertosJuego / totalIntentosJuego * 100f;
    }

    /// <summary>
    /// Calcula la precisión de las métricas recientes
    /// </summary>
    public float CalcularPrecisionReciente()
    {
        if (intentosRecientes.Count <= 0) return 100f;
        int aciertosRecientes = 0;
        foreach (var intento in intentosRecientes)
        {
            if (intento.fueCorrecta) aciertosRecientes++;
        }
        return (float)aciertosRecientes / intentosRecientes.Count * 100f;
    }

    /// <summary>
    /// Limpia solo los intentos recientes (para nueva ronda)
    /// </summary>
    public void LimpiarIntentosRecientes()
    {
        intentosRecientes.Clear();
        Debug.Log("PerformanceTracker: Intentos recientes limpiados");
    }

    /// <summary>
    /// Reinicia métricas de ZONA (al cambiar de zona)
    /// NO reinicia las métricas globales del juego
    /// </summary>
    public void ReiniciarMetricas()
    {
        // Solo reiniciar métricas de zona, NO las globales del juego
        intentosRecientes.Clear();
        totalIntentosZona = 0;
        totalAciertosZona = 0;
        tiempoPromedioZona = 0f;
        
        // NO limpiar historialCompleto - lo necesitamos para estadísticas
        // NO limpiar totalIntentosJuego, totalAciertosJuego, etc.
        
        OnMetricasActualizadas?.Invoke(ObtenerMetricasGlobales());
        
        Debug.Log($"PerformanceTracker: Métricas de zona reiniciadas (Juego total: {totalAciertosJuego}/{totalIntentosJuego})");
    }

    /// <summary>
    /// NUEVO: Reinicia TODO (al iniciar un juego completamente nuevo)
    /// </summary>
    public void ReiniciarJuegoCompleto()
    {
        historialCompleto.Clear();
        intentosRecientes.Clear();
        
        // Métricas de zona
        totalIntentosZona = 0;
        totalAciertosZona = 0;
        tiempoPromedioZona = 0f;
        
        // Métricas globales del juego
        totalIntentosJuego = 0;
        totalAciertosJuego = 0;
        sumaTiemposJuego = 0f;
        intentosConTiempoJuego = 0;
        
        OnMetricasActualizadas?.Invoke(ObtenerMetricasGlobales());
        
        Debug.Log("PerformanceTracker: TODO reiniciado para nuevo juego");
    }

    public List<RegistroIntento> ObtenerHistorialCompleto()
    {
        return new List<RegistroIntento>(historialCompleto);
    }
}
