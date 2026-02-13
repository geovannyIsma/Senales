using UnityEngine;
using System.Collections.Generic;

public class GeneradorDeSenales : MonoBehaviour
{
    [System.Serializable] // Esto permite que la clase se vea en el Inspector
    public class GrupoDeSenal
    {
        public string nombre;             // Nombre para organizarte (ej: "Señales de Pare")
        public GameObject prefabSenal;    // Arrastra aquí tu prefab (ej: senal_pare)
        public int cantidadAparecer = 1;  // ¿Cuántas de estas quieres que aparezcan?
        public Transform[] lugaresPosibles; // Arrastra aquí todos los Spawn_Pare_X
    }

    // Lista principal que verás en el Inspector
    public List<GrupoDeSenal> configuracionSenales;

    void Start()
    {
        GenerarSenales();
    }

    void GenerarSenales()
    {
        foreach (var grupo in configuracionSenales)
        {
            SpawnearGrupo(grupo);
        }
    }

    void SpawnearGrupo(GrupoDeSenal grupo)
    {
        // 1. Verificamos si hay suficientes lugares
        if (grupo.lugaresPosibles.Length == 0) return;

        // 2. Creamos una copia de la lista para ir tachando los lugares usados
        List<Transform> bolsaDeLugares = new List<Transform>(grupo.lugaresPosibles);

        // 3. Spawneamos la cantidad solicitada
        for (int i = 0; i < grupo.cantidadAparecer; i++)
        {
            if (bolsaDeLugares.Count == 0) break; // Se acabaron los lugares

            // Elegir un índice aleatorio de la bolsa
            int indiceAlAzar = Random.Range(0, bolsaDeLugares.Count);
            Transform lugarElegido = bolsaDeLugares[indiceAlAzar];

            // CREAR LA SEÑAL
            // Usamos la posición y rotación del Spawn Point
            Instantiate(grupo.prefabSenal, lugarElegido.position, lugarElegido.rotation);

            // Sacar este lugar de la bolsa para no repetir
            bolsaDeLugares.RemoveAt(indiceAlAzar);
        }
    }
}