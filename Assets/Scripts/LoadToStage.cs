using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadToStage : MonoBehaviour
{
    [SerializeField] GameObject fadeOut;

    void Start()
    {
        StartCoroutine(LoadLevel());
    }

    IEnumerator LoadLevel()
    {
        yield return new WaitForSeconds(1.15f);

        if (fadeOut != null)
            fadeOut.SetActive(true);

        yield return new WaitForSeconds(0.55f);
        SceneManager.LoadScene("CasinoRun");
    }
}
