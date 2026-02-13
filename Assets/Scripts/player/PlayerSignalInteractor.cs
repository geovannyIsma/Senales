using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR; // Necesario para detectar VR

public class PlayerSignalInteractor : MonoBehaviour
{
    [Header("Configuración de Raycast")]
    public Transform puntoOrigen; // La cámara o controlador VR
    public float distanciaMaxima = 50f;
    public LayerMask capaSenales;

    [Header("Visual")]
    public LineRenderer lineaRayo;
    public GameObject indicadorMira;
    
    [Header("Estado")]
    [SerializeField] private TrafficSign senalApuntada;
    
    // Para XR
    private bool usarControladorVR = true;
    private bool modoVrActivo = false;

    void Start()
    {
        // Detectar si hay un headset conectado para desactivar este script manual
        // CORRECCIÓN AQUÍ: Usamos UnityEngine.XR.InputDevice explícitamente
        var inputDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, inputDevices);
        modoVrActivo = inputDevices.Count > 0;

        if (modoVrActivo)
        {
            Debug.Log("Modo VR detectado: Desactivando Raycast manual (PlayerSignalInteractor) para usar XR Interaction Toolkit.");
            // Desactivamos este componente para dejar que XR Interaction Toolkit haga el trabajo
            this.enabled = false; 
            return; 
        }

        if (puntoOrigen == null)
        {
            puntoOrigen = Camera.main?.transform;
        }

        // Configurar línea del rayo si existe
        if (lineaRayo != null)
        {
            lineaRayo.positionCount = 2;
        }
    }

    void Update()
    {
        // Doble seguridad
        if (modoVrActivo) return;

        if (puntoOrigen == null) return;
        
        RealizarRaycast();
        DetectarInput();
    }

    void RealizarRaycast()
    {
        Ray rayo = new Ray(puntoOrigen.position, puntoOrigen.forward);
        RaycastHit hit;

        // Actualizar visual del rayo
        if (lineaRayo != null)
        {
            lineaRayo.SetPosition(0, puntoOrigen.position);
        }

        if (Physics.Raycast(rayo, out hit, distanciaMaxima, capaSenales))
        {
            // Encontramos algo
            TrafficSign senal = hit.collider.GetComponentInParent<TrafficSign>();
            
            if (senal != null && !senal.yaFueSeleccionada)
            {
                // Apuntando a una señal válida
                if (senalApuntada != senal)
                {
                    // Cambió la señal apuntada
                    DesresaltarSenalAnterior();
                    senalApuntada = senal;
                    ResaltarSenalActual();
                }
            }
            else
            {
                DesresaltarSenalAnterior();
                senalApuntada = null;
            }

            // Actualizar visual
            if (lineaRayo != null)
                lineaRayo.SetPosition(1, hit.point);
            
            if (indicadorMira != null)
            {
                indicadorMira.SetActive(true);
                indicadorMira.transform.position = hit.point;
                indicadorMira.transform.rotation = Quaternion.LookRotation(hit.normal);
            }
        }
        else
        {
            // No apunta a nada
            DesresaltarSenalAnterior();
            senalApuntada = null;

            if (lineaRayo != null)
                lineaRayo.SetPosition(1, puntoOrigen.position + puntoOrigen.forward * distanciaMaxima);
            
            if (indicadorMira != null)
                indicadorMira.SetActive(false);
        }
    }

    void DetectarInput()
    {
        // No procesar input si el menú de reconocimiento está activo
        if (RecognitionMenuUI.Instance != null && RecognitionMenuUI.Instance.EstaActivo())
        {
            return;
        }

        // Input de teclado/mouse para testing usando el nuevo Input System
        bool clickIzquierdo = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool teclaEspacio = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        
        if (clickIzquierdo || teclaEspacio)
        {
            SeleccionarSenalApuntada();
        }

        // TODO: Agregar input de controladores VR
        // if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
        // {
        //     SeleccionarSenalApuntada();
        // }
    }

    void SeleccionarSenalApuntada()
    {
        if (senalApuntada != null)
        {
            senalApuntada.Interactuar();
            senalApuntada = null;
        }
    }

    void ResaltarSenalActual()
    {
        if (senalApuntada == null) return;
        
        var highlight = senalApuntada.GetComponent<SignalHighlightController>();
        highlight?.ActivarResaltado();
    }

    void DesresaltarSenalAnterior()
    {
        if (senalApuntada == null) return;
        
        var highlight = senalApuntada.GetComponent<SignalHighlightController>();
        highlight?.DesactivarResaltado();
    }

    // Método para XR Interaction Toolkit
    public void OnSelectSignal(TrafficSign senal)
    {
        senal?.Interactuar();
    }
}
