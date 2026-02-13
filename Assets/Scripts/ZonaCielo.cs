using UnityEngine;

public class ZonaCielo : MonoBehaviour
{
    [Header("Configuraci�n")]
    public Transform objetoCielo;      // Aquí pondrás tu esfera gigante hothSky
    public Transform centroDeEstaZona; // El objeto vacío que creaste en el paso 1

    private void Start()
    {
        // Validación de referencias
        if (objetoCielo == null)
            Debug.LogError("[ZonaCielo] Falta asignar 'objetoCielo' en el Inspector.");
        if (centroDeEstaZona == null)
            Debug.LogError("[ZonaCielo] Falta asignar 'centroDeEstaZona' en el Inspector.");

        // Validación de Collider
        Collider col = GetComponent<Collider>();
        if (col == null)
            Debug.LogError("[ZonaCielo] Este objeto necesita un Collider (con Is Trigger activado).");
        else if (!col.isTrigger)
            Debug.LogWarning("[ZonaCielo] El Collider debe tener 'Is Trigger' activado.");
    }

    // Esta funcin se activa sola cuando algo entra en la caja invisible
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("[ZonaCielo] OnTriggerEnter detectado con: " + other.name);

        // Verificamos si lo que entró es el Jugador
        if (other.CompareTag("Player"))
        {
            if (objetoCielo != null && centroDeEstaZona != null)
            {
                Debug.Log($"[ZonaCielo] Posición actual del cielo: {objetoCielo.position}");
                Debug.Log($"[ZonaCielo] Centro de la zona: {centroDeEstaZona.position}");

                // Desemparentar el cielo para evitar problemas de posición local
                if (objetoCielo.parent != null)
                {
                    objetoCielo.SetParent(null);
                    Debug.Log("[ZonaCielo] Cielo desemparentado antes de mover.");
                }

                objetoCielo.position = centroDeEstaZona.position;

                Debug.Log($"[ZonaCielo] Nueva posición del cielo: {objetoCielo.position}");
                Debug.Log("[ZonaCielo] Cielo movido a: " + centroDeEstaZona.name);
            }
            else
            {
                Debug.LogWarning("[ZonaCielo] No se puede mover el cielo porque falta una referencia.");
            }
        }
    }
}