using UnityEngine;

/// <summary>
/// Manager centralizado para todos los efectos de sonido del juego
/// </summary>
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource audioSourceEfectos;
    public AudioSource audioSourceUI;
    public AudioSource audioSourceMusica; // NUEVO: Para música de fondo

    [Header("Música de Fondo")]
    public AudioClip musicaJuego;           // NUEVO: Música durante el gameplay
    public AudioClip musicaVictoria;        // NUEVO: Al completar zona/juego
    [Range(0f, 1f)] public float volumenMusica = 0.5f;
    public float duracionFadeMusica = 1f;   // NUEVO: Duración del fade in/out

    [Header("Sonidos de Interacción con Señales")]
    public AudioClip sonidoHover;
    public AudioClip sonidoSeleccionar;
    public AudioClip sonidoCorrecto;
    public AudioClip sonidoIncorrecto;
    public AudioClip sonidoTiempoAgotado;

    [Header("Sonidos de UI")]
    public AudioClip sonidoBotonUI;
    public AudioClip sonidoAbrirMenu;
    public AudioClip sonidoCerrarMenu;

    [Header("Sonidos de Progreso")]
    public AudioClip sonidoRondaCompletada;
    public AudioClip sonidoZonaCompletada;
    public AudioClip sonidoNuevoNivel;

    [Header("Configuración de Volumen")]
    [Range(0f, 1f)] public float volumenEfectos = 1f;
    [Range(0f, 1f)] public float volumenUI = 0.8f;
    [Range(0f, 1f)] public float volumenHover = 0.3f;

    private Coroutine corutinaFadeMusica;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Crear AudioSources si no existen
        if (audioSourceEfectos == null)
        {
            audioSourceEfectos = gameObject.AddComponent<AudioSource>();
            audioSourceEfectos.playOnAwake = false;
        }
        if (audioSourceUI == null)
        {
            audioSourceUI = gameObject.AddComponent<AudioSource>();
            audioSourceUI.playOnAwake = false;
        }
        // NUEVO: AudioSource para música
        if (audioSourceMusica == null)
        {
            audioSourceMusica = gameObject.AddComponent<AudioSource>();
            audioSourceMusica.playOnAwake = false;
            audioSourceMusica.loop = true;
            audioSourceMusica.volume = volumenMusica;
        }
    }

    void Start()
    {
        // Suscribirse a cambios de estado del juego
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnEstadoCambiado.AddListener(OnCambioEstadoJuego);
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnEstadoCambiado.RemoveListener(OnCambioEstadoJuego);
        }
    }

    // NUEVO: Reaccionar a cambios de estado del juego
    void OnCambioEstadoJuego(GameState nuevoEstado)
    {
        switch (nuevoEstado)
        {
            case GameState.MainMenu:
                // El MainMenuUI maneja su propia música
                DetenerMusicaJuego();
                break;
                
            case GameState.ZoneIntro:
                // Iniciar música del juego al entrar a una zona
                IniciarMusicaJuego();
                break;
                
            case GameState.GameComplete:
                // Cambiar a música de victoria
                if (musicaVictoria != null)
                {
                    CambiarMusica(musicaVictoria);
                }
                break;
        }
    }

    // ========== MÉTODOS DE MÚSICA ==========

    /// <summary>
    /// Inicia la música de fondo del juego
    /// </summary>
    public void IniciarMusicaJuego()
    {
        if (musicaJuego == null || audioSourceMusica == null) return;
        
        if (audioSourceMusica.clip == musicaJuego && audioSourceMusica.isPlaying)
            return; // Ya está sonando
        
        if (corutinaFadeMusica != null)
            StopCoroutine(corutinaFadeMusica);
        
        corutinaFadeMusica = StartCoroutine(FadeInMusica(musicaJuego));
        Debug.Log("GameAudioManager: Iniciando música del juego");
    }

    /// <summary>
    /// Detiene la música de fondo del juego
    /// </summary>
    public void DetenerMusicaJuego()
    {
        if (audioSourceMusica == null || !audioSourceMusica.isPlaying) return;
        
        if (corutinaFadeMusica != null)
            StopCoroutine(corutinaFadeMusica);
        
        corutinaFadeMusica = StartCoroutine(FadeOutMusica());
        Debug.Log("GameAudioManager: Deteniendo música del juego");
    }

    /// <summary>
    /// Cambia a otra pista de música con fade
    /// </summary>
    public void CambiarMusica(AudioClip nuevaMusica)
    {
        if (audioSourceMusica == null || nuevaMusica == null) return;
        
        if (corutinaFadeMusica != null)
            StopCoroutine(corutinaFadeMusica);
        
        corutinaFadeMusica = StartCoroutine(CrossfadeMusica(nuevaMusica));
    }

    /// <summary>
    /// Pausa/reanuda la música
    /// </summary>
    public void PausarMusica(bool pausar)
    {
        if (audioSourceMusica == null) return;
        
        if (pausar)
            audioSourceMusica.Pause();
        else
            audioSourceMusica.UnPause();
    }

    /// <summary>
    /// Ajusta el volumen de la música
    /// </summary>
    public void SetVolumenMusica(float volumen)
    {
        volumenMusica = Mathf.Clamp01(volumen);
        if (audioSourceMusica != null)
            audioSourceMusica.volume = volumenMusica;
    }

    // Coroutines para transiciones suaves
    System.Collections.IEnumerator FadeInMusica(AudioClip clip)
    {
        audioSourceMusica.clip = clip;
        audioSourceMusica.volume = 0f;
        audioSourceMusica.Play();

        float tiempo = 0f;
        while (tiempo < duracionFadeMusica)
        {
            tiempo += Time.deltaTime;
            audioSourceMusica.volume = Mathf.Lerp(0f, volumenMusica, tiempo / duracionFadeMusica);
            yield return null;
        }
        audioSourceMusica.volume = volumenMusica;
    }

    System.Collections.IEnumerator FadeOutMusica()
    {
        float volumenInicial = audioSourceMusica.volume;
        float tiempo = 0f;
        
        while (tiempo < duracionFadeMusica)
        {
            tiempo += Time.deltaTime;
            audioSourceMusica.volume = Mathf.Lerp(volumenInicial, 0f, tiempo / duracionFadeMusica);
            yield return null;
        }
        
        audioSourceMusica.Stop();
        audioSourceMusica.volume = volumenMusica;
    }

    System.Collections.IEnumerator CrossfadeMusica(AudioClip nuevaMusica)
    {
        // Fade out
        float volumenInicial = audioSourceMusica.volume;
        float tiempo = 0f;
        
        while (tiempo < duracionFadeMusica * 0.5f)
        {
            tiempo += Time.deltaTime;
            audioSourceMusica.volume = Mathf.Lerp(volumenInicial, 0f, tiempo / (duracionFadeMusica * 0.5f));
            yield return null;
        }
        
        // Cambiar clip
        audioSourceMusica.Stop();
        audioSourceMusica.clip = nuevaMusica;
        audioSourceMusica.Play();
        
        // Fade in
        tiempo = 0f;
        while (tiempo < duracionFadeMusica * 0.5f)
        {
            tiempo += Time.deltaTime;
            audioSourceMusica.volume = Mathf.Lerp(0f, volumenMusica, tiempo / (duracionFadeMusica * 0.5f));
            yield return null;
        }
        audioSourceMusica.volume = volumenMusica;
    }

    // ========== MÉTODOS DE EFECTOS (sin cambios) ==========

    public void ReproducirHover()
    {
        ReproducirEfecto(sonidoHover, volumenHover);
    }

    public void ReproducirSeleccion()
    {
        ReproducirEfecto(sonidoSeleccionar, volumenEfectos);
    }

    public void ReproducirCorrecto()
    {
        ReproducirEfecto(sonidoCorrecto, volumenEfectos);
    }

    public void ReproducirIncorrecto()
    {
        ReproducirEfecto(sonidoIncorrecto, volumenEfectos);
    }

    public void ReproducirTiempoAgotado()
    {
        ReproducirEfecto(sonidoTiempoAgotado, volumenEfectos);
    }

    public void ReproducirBotonUI()
    {
        ReproducirUI(sonidoBotonUI, volumenUI);
    }

    public void ReproducirAbrirMenu()
    {
        ReproducirUI(sonidoAbrirMenu, volumenUI);
    }

    public void ReproducirCerrarMenu()
    {
        ReproducirUI(sonidoCerrarMenu, volumenUI);
    }

    public void ReproducirRondaCompletada()
    {
        ReproducirEfecto(sonidoRondaCompletada, volumenEfectos);
    }

    public void ReproducirZonaCompletada()
    {
        ReproducirEfecto(sonidoZonaCompletada, volumenEfectos);
    }

    void ReproducirEfecto(AudioClip clip, float volumen)
    {
        if (clip == null || audioSourceEfectos == null) return;
        audioSourceEfectos.PlayOneShot(clip, volumen);
    }

    void ReproducirUI(AudioClip clip, float volumen)
    {
        if (clip == null || audioSourceUI == null) return;
        audioSourceUI.PlayOneShot(clip, volumen);
    }

    public void ReproducirEnPosicion(AudioClip clip, Vector3 posicion, float volumen = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, posicion, volumen * volumenEfectos);
    }
}
