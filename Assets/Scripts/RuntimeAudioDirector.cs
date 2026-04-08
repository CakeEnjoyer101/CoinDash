using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-400)]
public sealed class RuntimeAudioDirector : MonoBehaviour
{
    static RuntimeAudioDirector instance;
    static AudioClip menuMusicClip;
    static AudioClip stageSelectMusicClip;
    static AudioClip runMusicClip;
    static AudioClip coinCollectClip;
    static AudioClip collisionClip;

    AudioSource musicSource;
    AudioSource fxSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static RuntimeAudioDirector EnsureExists()
    {
        if (instance != null)
            return instance;

        var go = new GameObject("RuntimeAudioDirector");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RuntimeAudioDirector>();
        return instance;
    }

    public static void PlayCoinCollect()
    {
        var director = EnsureExists();
        director.PlayFx(GetCoinCollectClip(), 0.92f);
    }

    public static void PlayCollision()
    {
        var director = EnsureExists();
        director.PlayFx(GetCollisionClip(), 1.12f);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSources();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        ApplySceneAudio(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplySceneAudioNextFrame(scene.name));
    }

    IEnumerator ApplySceneAudioNextFrame(string sceneName)
    {
        yield return null;
        ApplySceneAudio(sceneName);
    }

    void EnsureSources()
    {
        if (musicSource == null)
        {
            var musicObject = new GameObject("MusicSource");
            musicObject.transform.SetParent(transform, false);
            musicSource = musicObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        if (fxSource == null)
        {
            var fxObject = new GameObject("FxSource");
            fxObject.transform.SetParent(transform, false);
            fxSource = fxObject.AddComponent<AudioSource>();
            fxSource.playOnAwake = false;
            fxSource.loop = false;
            fxSource.spatialBlend = 0f;
        }
    }

    void ApplySceneAudio(string sceneName)
    {
        EnsureSources();
        SilenceSceneAudioSources();

        AudioClip targetClip = sceneName switch
        {
            "MainMenu" => GetMenuMusicClip(),
            "StageSelect" => GetStageSelectMusicClip(),
            "CasinoRun" => GetRunMusicClip(),
            _ => null
        };

        if (targetClip == null)
        {
            if (sceneName == "LoadingScene")
                return;

            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        musicSource.loop = true;
        musicSource.volume = sceneName == "CasinoRun" ? 0.22f : 0.54f;
        if (musicSource.clip != targetClip)
        {
            musicSource.Stop();
            musicSource.clip = targetClip;
            musicSource.Play();
            return;
        }

        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    void SilenceSceneAudioSources()
    {
        var sources = FindObjectsOfType<AudioSource>(true);
        foreach (var source in sources)
        {
            if (source == null)
                continue;

            if (source == musicSource || source == fxSource)
                continue;

            if (source.transform.IsChildOf(transform))
                continue;

            source.Stop();
            source.playOnAwake = false;
            source.mute = true;
        }
    }

    void PlayFx(AudioClip clip, float volume)
    {
        EnsureSources();
        if (clip == null)
            return;

        fxSource.PlayOneShot(clip, volume);
    }

    static AudioClip GetMenuMusicClip()
    {
        if (menuMusicClip == null)
            menuMusicClip = Resources.Load<AudioClip>("Audio/BGM/MenuBackgroundSong");
        return menuMusicClip;
    }

    static AudioClip GetStageSelectMusicClip()
    {
        if (stageSelectMusicClip == null)
            stageSelectMusicClip = Resources.Load<AudioClip>("Audio/BGM/StageSelectBackgroundMusic");
        return stageSelectMusicClip;
    }

    static AudioClip GetRunMusicClip()
    {
        if (runMusicClip == null)
            runMusicClip = Resources.Load<AudioClip>("Audio/BGM/InGameMusicNeonCasinoMap");
        return runMusicClip;
    }

    static AudioClip GetCoinCollectClip()
    {
        if (coinCollectClip == null)
            coinCollectClip = Resources.Load<AudioClip>("Audio/FX/CoinCollect");
        return coinCollectClip;
    }

    static AudioClip GetCollisionClip()
    {
        if (collisionClip == null)
            collisionClip = Resources.Load<AudioClip>("Audio/FX/Collision");
        return collisionClip;
    }
}
