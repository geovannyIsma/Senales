using UnityEngine;
using System.Collections.Generic;

public class ZoneController : MonoBehaviour
{
    [Header("Información de Zona")]
    public string nombreZona;


    [Header("Señales Disponibles en esta Zona")]
    public List<TrafficSignData> senalesDisponibles;

    [Header("Objetos de la Zona")]
    public GameObject contenedorVisual;  // Todo lo visual de la zona
    public Transform puntoSpawnJugador;  // Dónde aparece el jugador

    [Header("Configuración de Teletransporte")]
    public bool teletransportarAlActivar = true;
    public Transform xrOriginOverride; // NUEVO: Asignar manualmente si la búsqueda automática falla

    [Header("Estado")]
    [SerializeField] private bool activa = false;
    public bool EstaActiva => activa;

    private SignalSpawner miSpawner;

    void Awake()
    {
        miSpawner = GetComponent<SignalSpawner>();
    }

    public void ActivarZona()
    {
        activa = true;
        
        if (contenedorVisual != null)
            contenedorVisual.SetActive(true);
        
        // Teletransportar jugador si es necesario
        if (teletransportarAlActivar && puntoSpawnJugador != null)
        {
            TeletransportarJugador();
        }

        Debug.Log($"Zona activada: {nombreZona}");
    }

    void TeletransportarJugador()
    {
        // Buscar el XR Origin (rig de VR)
        Transform xrOrigin = ObtenerXROrigin();
        
        if (xrOrigin == null)
        {
            Debug.LogError($"[{nombreZona}] No se encontró XR Origin para teletransportar");
            return;
        }

        // Obtener la cámara para calcular el offset
        Camera camaraVR = Camera.main;
        if (camaraVR == null)
        {
            camaraVR = xrOrigin.GetComponentInChildren<Camera>();
        }

        if (camaraVR != null)
        {
            // Calcular el offset horizontal entre el XR Origin y la cámara
            // Esto es necesario porque en VR el jugador puede moverse dentro del espacio de juego
            Vector3 offsetCamara = xrOrigin.position - camaraVR.transform.position;
            offsetCamara.y = 0; // Solo offset horizontal
            
            // Posición final = punto de spawn + offset para que la CÁMARA quede en el spawn
            Vector3 posicionFinal = puntoSpawnJugador.position + offsetCamara;
            
            // Mantener la altura del XR Origin (el suelo del rig)
            posicionFinal.y = puntoSpawnJugador.position.y;
            
            xrOrigin.position = posicionFinal;
            
            Debug.Log($"[{nombreZona}] Jugador teletransportado a {posicionFinal} (spawn: {puntoSpawnJugador.position}, offset: {offsetCamara})");
        }
        else
        {
            // Sin cámara, mover directamente
            xrOrigin.position = puntoSpawnJugador.position;
            Debug.Log($"[{nombreZona}] Jugador teletransportado a {puntoSpawnJugador.position} (sin offset de cámara)");
        }

        // Aplicar rotación si es necesario
        // Nota: En VR normalmente solo rotamos el rig en Y para no desorientar al jugador
        Vector3 rotacionActual = xrOrigin.eulerAngles;
        xrOrigin.rotation = Quaternion.Euler(rotacionActual.x, puntoSpawnJugador.eulerAngles.y, rotacionActual.z);
    }

    Transform ObtenerXROrigin()
    {
        // 1. Usar override manual si está asignado
        if (xrOriginOverride != null)
        {
            return xrOriginOverride;
        }

        // 2. Buscar por componente XR Origin (Unity XR Interaction Toolkit)
        var xrOriginComponent = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOriginComponent != null)
        {
            return xrOriginComponent.transform;
        }

        // 3. Buscar por nombre común
        string[] nombresComunes = { "XR Origin", "XROrigin", "XR Rig", "XRRig", "XR Origin (XR Rig)" };
        foreach (string nombre in nombresComunes)
        {
            GameObject obj = GameObject.Find(nombre);
            if (obj != null)
            {
                return obj.transform;
            }
        }

        // 4. Buscar objeto con tag "Player" y subir al padre raíz
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador != null)
        {
            // Subir hasta encontrar el rig raíz
            Transform actual = jugador.transform;
            while (actual.parent != null)
            {
                actual = actual.parent;
            }
            return actual;
        }

        // 5. Último recurso: buscar la cámara principal y su rig
        Camera camara = Camera.main;
        if (camara != null)
        {
            Transform actual = camara.transform;
            while (actual.parent != null)
            {
                actual = actual.parent;
            }
            return actual;
        }

        return null;
    }

    public void DesactivarZona()
    {
        activa = false;
        
        if (contenedorVisual != null)
            contenedorVisual.SetActive(false);
        
        // Limpiar señales
        miSpawner?.LimpiarSenalesAnteriores();

        Debug.Log($"Zona desactivada: {nombreZona}");
    }
}
