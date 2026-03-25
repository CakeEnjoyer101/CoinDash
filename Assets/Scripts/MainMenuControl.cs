using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuControl : MonoBehaviour
{
    [SerializeField] GameObject fadeOut;
    [SerializeField] GameObject bounceText;
    [SerializeField] GameObject bigButton;
    [SerializeField] GameObject animCam;
    [SerializeField] GameObject mainCam;
    [SerializeField] GameObject menuControls;
    public static bool hasClicked;
    [SerializeField] GameObject staticCam;
    [SerializeField] GameObject fadeIn;
    // Start is called before the first frame update
    void Start()
    {
        if (fadeIn != null)
            fadeIn.SetActive(false);

        if (hasClicked)
        {
            staticCam.SetActive(true);
            animCam.SetActive(false);
            menuControls.SetActive(true);
            bounceText.SetActive(false);
            bigButton.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void MenuBeginButton()
    {
        if (fadeIn != null)
            fadeIn.SetActive(false);

        if (mainCam != null)
            mainCam.SetActive(true);

        if (animCam != null)
            animCam.SetActive(false);

        if (menuControls != null)
            menuControls.SetActive(true);

        if (bounceText != null)
            bounceText.SetActive(false);

        if (bigButton != null)
            bigButton.SetActive(false);

        hasClicked = true;
    }


    public void StartGame()
    {
        SceneManager.LoadScene(2);
    }
}
