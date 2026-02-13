using UnityEngine;
using UnityEngine.Events;

public class TimerManager : MonoBehaviour
{
    [Header("Estado")]
    [SerializeField] private float tiempoRestante;
    [SerializeField] private bool activo = false;

    public float TiempoRestante => tiempoRestante;
    public bool EstaActivo => activo;

    [Header("Eventos")]
    public UnityEvent<float> OnTiempoActualizado; // Envía tiempo restante
    public UnityEvent OnTiempoAgotado;
    public UnityEvent<float> OnAdvertenciaTiempo; // Cuando queda poco tiempo

    [Header("Configuración")]
    public float tiempoAdvertencia = 5f;
    private bool advertenciaEmitida = false;

    // Update is called once per frame
    void Update()
    {
        if (!activo) return;

        tiempoRestante -= Time.deltaTime;
        OnTiempoActualizado?.Invoke(tiempoRestante);

        // Advertencia de poco tiempo
        if (!advertenciaEmitida && tiempoRestante <= tiempoAdvertencia)
        {
            advertenciaEmitida = true;
            OnAdvertenciaTiempo?.Invoke(tiempoRestante);
        }

        // Tiempo agotado
        if (tiempoRestante <= 0)
        {
            tiempoRestante = 0;
            activo = false;
            
            // NUEVO: Reproducir sonido de tiempo agotado
            GameAudioManager.Instance?.ReproducirTiempoAgotado();
            
            OnTiempoAgotado?.Invoke();
            GameManager.Instance?.TiempoAgotado();
        }
    }

    public void IniciarTemporizador(float segundos)
    {
        tiempoRestante = segundos;
        activo = true;
        advertenciaEmitida = false;
        
        // FIX: Actualizar UI inmediatamente para que no empiece "vacío" o con el número anterior
        OnTiempoActualizado?.Invoke(tiempoRestante);
        
        Debug.Log($"Temporizador iniciado: {segundos}s");
    }

    // NUEVO MÉTODO: Configura el tiempo pero lo deja en pausa
    public void ConfigurarTemporizador(float segundos)
    {
        tiempoRestante = segundos;
        activo = false; // Importante: empieza pausado
        advertenciaEmitida = false;
        OnTiempoActualizado?.Invoke(tiempoRestante); // Actualiza la UI con el tiempo total
        Debug.Log($"Temporizador configurado (esperando inicio): {segundos}s");
    }

    public void DetenerTemporizador()
    {
        activo = false;
    }

    public void PausarTemporizador()
    {
        activo = false;
    }

    public void ReanudarTemporizador()
    {
        if (tiempoRestante > 0)
            activo = true;
    }
}
