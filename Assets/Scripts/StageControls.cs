using UnityEngine;
using UnityEngine.SceneManagement;

public class StageControls : MonoBehaviour
{
    public void PressPlay()
    {
        PlayStage(0);
    }

    public void PressPlaySecond()
    {
        PlayStage(1);
    }

    public void pressPlay()
    {
        PlayStage(0);
    }

    void PlayStage(int stageIndex)
    {
        RunGameplayDirector.SetSelectedStage(stageIndex);
        SceneManager.LoadScene("LoadingScene");
    }
}
