using UnityEngine;
using System.Collections;

/// <summary>
/// Controla el resaltado visual de las señales cuando el jugador las apunta con el control VR
/// Usa indicador flotante y HALO DE PARTÍCULAS (Sin luces).
/// </summary>
public class SignalHighlightController : MonoBehaviour
{
    [Header("Configuración de Colores")]
    public Color colorResaltado = new Color(1f, 1f, 0.5f, 1f); // Amarillo claro
    public Color colorSeleccion = new Color(0.32f, 0.19f, 0.94f, 1f); // 5231F0
    public Color colorCorrecto = Color.green;
    public Color colorIncorrecto = Color.red;
    
    [Header("Indicador Visual Flotante")]
    public bool usarIndicadorFlotante = true;
    public GameObject prefabIndicador;
    public Vector3 offsetIndicador = new Vector3(1.2f, 1.0f, 0); 
    public float velocidadRotacionIndicador = 90f;
    public float escalaIndicador = 0.5f; 
    public Material materialIndicadorFallback;
    
    [Header("Efecto Halo (GLOW VISIBLE EN AIRE)")]
    public bool usarHalo = true;
    public float tamanoHalo = 2f; 
    [Range(0f, 1f)] public float opacidadHalo = 0.6f; // Que tan transparente es
    
    [Header("Material Partículas (IMPORTANTE PARA QUEST)")]
    [Tooltip("Asigna un material con shader de partículas. Si está vacío, intentará crear uno automáticamente.")]
    public Material materialParticulasVR; // Asignar desde Inspector para garantizar que funcione en Quest

    [Header("Estado")]
    [SerializeField] private bool resaltadoActivo = false;
    [SerializeField] private bool seleccionado = false;

    // Componentes
    private GameObject indicadorInstanciado;
    private ParticleSystem haloSystem; 
    private Texture2D texturaHaloGenerada;
    
    // Para efectos
    private Coroutine coroutinaPulso;
    private Coroutine corutinaResultado;

    void Awake()
    {
        CrearComponentesVisuales();
    }

    void CrearComponentesVisuales()
    {
        // 1. Crear Halo de Partículas (El resplandor visible en el aire)
        if (usarHalo)
        {
            CrearHaloParticulas();
        }

        // 2. Crear indicador flotante
        if (usarIndicadorFlotante && prefabIndicador != null)
        {
            indicadorInstanciado = Instantiate(prefabIndicador, transform);
            indicadorInstanciado.transform.localPosition = offsetIndicador;
            indicadorInstanciado.SetActive(false);
        }
        else if (usarIndicadorFlotante)
        {
            CrearIndicadorSimple();
        }
    }

    void CrearHaloParticulas()
    {
        GameObject haloObj = new GameObject("HaloGlow");
        haloObj.transform.SetParent(transform);
        haloObj.transform.localPosition = offsetIndicador; 

        haloSystem = haloObj.AddComponent<ParticleSystem>();
        var renderer = haloObj.GetComponent<ParticleSystemRenderer>();
        
        // ======= SOLUCIÓN PARA QUEST (Built-in RP) =======
        Material particleMat = null;
        
        // OPCIÓN 1: Usar material asignado desde Inspector (RECOMENDADO)
        if (materialParticulasVR != null)
        {
            particleMat = new Material(materialParticulasVR);
            Debug.Log("[SignalHighlight] Usando material asignado desde Inspector");
        }
        else
        {
            // OPCIÓN 2: Buscar shaders compatibles con Built-in RP
            Shader shaderParticula = null;
            
            // Lista de shaders para Built-in Render Pipeline
            string[] shadersAProbar = new string[]
            {
                "Legacy Shaders/Particles/Alpha Blended",     // Legacy - más compatible
                "Mobile/Particles/Alpha Blended",              // Mobile
                "Particles/Standard Unlit",                    // Standard
                "Particles/Alpha Blended Premultiply",         // Alternativo
                "Sprites/Default"                              // Último recurso
            };
            
            foreach (string nombreShader in shadersAProbar)
            {
                shaderParticula = Shader.Find(nombreShader);
                if (shaderParticula != null)
                {
                    Debug.Log($"[SignalHighlight] Shader encontrado: {nombreShader}");
                    break;
                }
            }
            
            if (shaderParticula == null)
            {
                Debug.LogWarning("[SignalHighlight] No se encontró shader de partículas. Asigna 'materialParticulasVR' en el Inspector.");
                // Usar Sprites/Default como último recurso
                shaderParticula = Shader.Find("Sprites/Default");
            }
            
            particleMat = new Material(shaderParticula);
        }
        
        if (texturaHaloGenerada == null) texturaHaloGenerada = GenerarTexturaCirculoSuave();
        particleMat.mainTexture = texturaHaloGenerada;
        
        renderer.material = particleMat;

        // --- Configuración del Sistema de Partículas ---
        var main = haloSystem.main;
        // CAMBIO: Reducido de 2.5f a 1.2f para que aparezcan/desaparezcan más rápido
        main.startLifetime = 0.8f; 
        main.startSpeed = 0f;      
        main.startSize = tamanoHalo; 
        main.maxParticles = 15;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        
        // Emisión
        var emission = haloSystem.emission;
        // CAMBIO: Aumentado levemente para compensar la vida más corta
        emission.rateOverTime = 5f; 

        var rotOL = haloSystem.rotationOverLifetime;
        rotOL.enabled = true;
        rotOL.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        // Color Transparente (Fade In/Out)
        var colorOL = haloSystem.colorOverLifetime;
        colorOL.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0f, 0f), 
                new GradientAlphaKey(opacidadHalo, 0.5f), 
                new GradientAlphaKey(0f, 1f) 
            }
        );
        colorOL.color = grad;

        haloObj.SetActive(false);
    }
    
    Texture2D GenerarTexturaCirculoSuave()
    {
        int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[res * res];
        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        float radius = res * 0.45f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.Clamp01(dist / radius);
                alpha = Mathf.Pow(alpha, 2); 
                
                pixels[y * res + x] = new Color(1, 1, 1, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    void CrearIndicadorSimple()
    {
        indicadorInstanciado = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicadorInstanciado.name = "IndicadorResaltado";
        indicadorInstanciado.transform.SetParent(transform);
        indicadorInstanciado.transform.localPosition = offsetIndicador;
        indicadorInstanciado.transform.localScale = Vector3.one * escalaIndicador;
        
        var col = indicadorInstanciado.GetComponent<Collider>();
        if (col != null) Destroy(col);
        
        var renderer = indicadorInstanciado.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard")); 
            mat.color = colorResaltado;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", colorResaltado * 2f);
            renderer.material = mat;
        }
        
        indicadorInstanciado.SetActive(false);
    }

    void Update()
    {
        if (indicadorInstanciado != null && indicadorInstanciado.activeSelf)
        {
            indicadorInstanciado.transform.Rotate(Vector3.up, velocidadRotacionIndicador * Time.deltaTime);
            float offset = Mathf.Sin(Time.time * 2f) * 0.15f;
            indicadorInstanciado.transform.localPosition = offsetIndicador + Vector3.up * offset;
        }
    }

    /// <summary>
    /// Activa el resaltado cuando el jugador apunta a la señal
    /// </summary>
    public void ActivarResaltado()
    {
        if (resaltadoActivo || seleccionado) return;
        
        resaltadoActivo = true;
        
        // NUEVO: Reproducir sonido de hover
        GameAudioManager.Instance?.ReproducirHover();
        
        ActualizarVisuales(colorResaltado);
        
        if (coroutinaPulso != null) StopCoroutine(coroutinaPulso);
        coroutinaPulso = StartCoroutine(EfectoPulso());
    }

    /// <summary>
    /// Desactiva el resaltado cuando el jugador deja de apuntar
    /// </summary>
    public void DesactivarResaltado()
    {
        // Si está seleccionado (menú abierto), NO desactivamos visuales
        if (!resaltadoActivo || seleccionado) return;
        
        resaltadoActivo = false;
        
        if (coroutinaPulso != null)
        {
            StopCoroutine(coroutinaPulso);
            coroutinaPulso = null;
        }
        
        // Apagar todo
        if (indicadorInstanciado != null) indicadorInstanciado.SetActive(false);
        if (haloSystem != null) haloSystem.gameObject.SetActive(false);
    }

    /// <summary>
    /// Muestra efecto visual cuando el jugador selecciona la señal
    /// </summary>
    public void MostrarSeleccion()
    {
        seleccionado = true;
        resaltadoActivo = false;
        
        if (coroutinaPulso != null)
        {
            StopCoroutine(coroutinaPulso);
            coroutinaPulso = null;
        }
        
        ActualizarVisuales(colorSeleccion);
    }

    /// <summary>
    /// Muestra el resultado (correcto/incorrecto) con feedback visual
    /// </summary>
    public void MostrarResultado(bool esCorrecto)
    {
        // Forzamos activación visual
        if (indicadorInstanciado != null) indicadorInstanciado.SetActive(true);
        if (haloSystem != null) haloSystem.gameObject.SetActive(true);

        if (corutinaResultado != null)
            StopCoroutine(corutinaResultado);
        
        corutinaResultado = StartCoroutine(EfectoResultado(esCorrecto));
    }

    void ActualizarVisuales(Color color)
    {
        // 1. Halo de Partículas
        if (haloSystem != null)
        {
            haloSystem.gameObject.SetActive(true);
            var main = haloSystem.main;
            main.startColor = color;
        }

        // 2. Indicador y Color Emisivo
        if (indicadorInstanciado != null)
        {
            indicadorInstanciado.SetActive(true);
            var renderer = indicadorInstanciado.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", color * 5f); // Muy brillante
                }
            }
        }
    }

    IEnumerator EfectoPulso()
    {
        float tiempo = 0f;
        while (resaltadoActivo)
        {
            tiempo += Time.deltaTime * 1.5f; // Un poco más rápido que antes
            float pulso = (Mathf.Sin(tiempo) + 1f) * 0.5f;
            
            // Pulsar tamaño
            if (haloSystem != null)
            {
                var main = haloSystem.main;
                main.startSize = Mathf.Lerp(tamanoHalo * 0.9f, tamanoHalo * 1.0f, pulso);
            }
            
            yield return null;
        }
    }

    IEnumerator EfectoResultado(bool esCorrecto)
    {
        Color colorResultado = esCorrecto ? colorCorrecto : colorIncorrecto;
        
        for (int i = 0; i < 4; i++) 
        {
            ActualizarVisuales(colorResultado);
            yield return new WaitForSeconds(0.15f);
            
            // Simular parpadeo apagando temporalmente
            if (haloSystem != null) haloSystem.gameObject.SetActive(false);
            if (indicadorInstanciado != null) indicadorInstanciado.SetActive(false);
            
            yield return new WaitForSeconds(0.15f);
        }
        
        ActualizarVisuales(colorResultado);
    }

    void OnDisable()
    {
        if (indicadorInstanciado != null) indicadorInstanciado.SetActive(false);
        if (haloSystem != null) haloSystem.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (indicadorInstanciado != null) Destroy(indicadorInstanciado);
    }
}
