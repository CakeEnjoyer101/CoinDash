using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadToStage : MonoBehaviour
{
    [SerializeField] GameObject fadeOut;
    void Start()
    {
        StartCoroutine(LoadLevel());
    }

    void Update()
    {
        
    }

    IEnumerator LoadLevel()
    {
        yield return new WaitForSeconds(3);
        fadeOut.SetActive(true);
        yield return new WaitForSeconds(1);
        SceneManager.LoadScene(1);
    }
}
