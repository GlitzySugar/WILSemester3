using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;

[DefaultExecutionOrder(-100)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music Clips")]
    public AudioClip mainMenuClip;
    public AudioClip gameLoopClip;

    [Header("Audio Sources & Settings")]
    // Two sources so we can crossfade between them
    private AudioSource srcA;
    private AudioSource srcB;
    private AudioSource sfxSource;

    public float musicVolume = 1f;
    [Tooltip("Seconds to crossfade between tracks")]
    public float crossfadeDuration = 1f;

    [Header("Optional")]
    public string mainMenuSceneName = "MainMenu"; // change to match your scene name
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup sfxMixerGroup;

    private AudioSource activeSource;
    private AudioSource idleSource;
    private Coroutine crossfadeCoroutine;

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create audio sources
        srcA = gameObject.AddComponent<AudioSource>();
        srcB = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();

        srcA.loop = true;
        srcB.loop = true;

        srcA.playOnAwake = false;
        srcB.playOnAwake = false;
        sfxSource.playOnAwake = false;

        if (musicMixerGroup != null)
        {
            srcA.outputAudioMixerGroup = musicMixerGroup;
            srcB.outputAudioMixerGroup = musicMixerGroup;
        }
        if (sfxMixerGroup != null) sfxSource.outputAudioMixerGroup = sfxMixerGroup;

        activeSource = srcA;
        idleSource = srcB;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // Start the correct music depending on the initial scene
        PlayMusicForScene(SceneManager.GetActiveScene().name, instant: true);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.name);
    }

    // Determines which music to play for the given scene name
    private void PlayMusicForScene(string sceneName, bool instant = false)
    {
        // Play main menu music
        if (mainMenuClip != null && sceneName == mainMenuSceneName)
        {
            PlayMusic(mainMenuClip, loop: true, instant: instant);
        }
        else
        {
            // Play game loop music
            if (gameLoopClip != null)
                PlayMusic(gameLoopClip, loop: true, instant: instant);
            else
                StopMusic();
        }
    }



    /// <summary>
    /// Play a music clip (crossfades from the currently playing music).
    /// If instant==true, switches immediately with no crossfade.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true, bool instant = false)
    {
        if (clip == null) return;

        // If the same clip is already playing on active source, do nothing
        if (activeSource.clip == clip && activeSource.isPlaying) return;

        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);

        idleSource.clip = clip;
        idleSource.loop = loop;
        idleSource.volume = 0f;
        idleSource.Play();

        if (instant || crossfadeDuration <= 0f)
        {
            // Immediately switch
            activeSource.Stop();
            idleSource.volume = musicVolume;
            SwapActiveIdle();
        }
        else
        {
            crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(crossfadeDuration));
        }
    }

    /// <summary>
    /// Force the main menu music to play (useful for UI buttons)
    /// </summary>
    public void PlayMainMenuMusic(bool instant = false) => PlayMusic(mainMenuClip, loop: true, instant: instant);

    /// <summary>
    /// Force the in-game loop music to play
    /// </summary>
    public void PlayGameLoopMusic(bool instant = false) => PlayMusic(gameLoopClip, loop: true, instant: instant);

    public void StopMusic(bool fadeOut = true)
    {
        if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
        if (fadeOut && activeSource.isPlaying && crossfadeDuration > 0f)
        {
            StartCoroutine(FadeOutAndStop(activeSource, crossfadeDuration));
        }
        else
        {
            activeSource.Stop();
            idleSource.Stop();
        }
    }

    IEnumerator CrossfadeRoutine(float duration)
    {
        float t = 0f;
        float startActiveVol = activeSource.volume;
        while (t < duration)
        {
            t += Time.deltaTime;
            float frac = t / duration;
            activeSource.volume = Mathf.Lerp(startActiveVol, 0f, frac);
            idleSource.volume = Mathf.Lerp(0f, musicVolume, frac);
            yield return null;
        }
        activeSource.Stop();
        activeSource.volume = 0f;
        idleSource.volume = musicVolume;

        SwapActiveIdle();
        crossfadeCoroutine = null;
    }

    IEnumerator FadeOutAndStop(AudioSource src, float duration)
    {
        float start = src.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            src.volume = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }
        src.Stop();
        src.volume = start; // restore - caller may depend on this
    }

    private void SwapActiveIdle()
    {
        var tmp = activeSource;
        activeSource = idleSource;
        idleSource = tmp;
    }

    /// <summary>
    /// Play a sound effect (doesn't affect music)
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>
    /// Change music global volume (0..1)
    /// </summary>
    public void SetMusicVolume(float vol)
    {
        musicVolume = Mathf.Clamp01(vol);
        activeSource.volume = musicVolume;
        idleSource.volume = musicVolume;
    }

    /// <summary>
    /// Change sfx volume
    /// </summary>
    public void SetSFXVolume(float vol)
    {
        sfxSource.volume = Mathf.Clamp01(vol);
    }
}
