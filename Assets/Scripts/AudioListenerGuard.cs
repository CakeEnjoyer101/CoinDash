using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
public sealed class AudioListenerGuard : MonoBehaviour
{
    static AudioListenerGuard instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject("AudioListenerGuard");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AudioListenerGuard>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        EnforceSingleListener();
    }

    void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnforceSingleListener();
    }

    void EnforceSingleListener()
    {
        var listeners = FindObjectsOfType<AudioListener>(true);
        if (listeners.Length <= 1)
            return;

        AudioListener preferred = null;

        foreach (var listener in listeners)
        {
            if (!listener.gameObject.activeInHierarchy)
                continue;

            if (listener.GetComponent<Camera>() != null && listener.GetComponent<Camera>().CompareTag("MainCamera"))
            {
                preferred = listener;
                break;
            }

            if (preferred == null)
                preferred = listener;
        }

        if (preferred == null)
            preferred = listeners[0];

        foreach (var listener in listeners)
            listener.enabled = listener == preferred;
    }
}
