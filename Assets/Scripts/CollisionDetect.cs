using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CollisionDetect : MonoBehaviour
{
    [SerializeField] GameObject thePlayer;
    [SerializeField] AudioSource collisionFX;
    [SerializeField] GameObject mainCam;
    [SerializeField] GameObject fadeOut;
    bool isProcessing;
    float reviveInvulnerabilityUntil;
    PlayerMovement playerMovement;

    void Awake()
    {
        if (thePlayer == null)
        {
            var player = FindObjectOfType<PlayerMovement>(true);
            if (player != null)
                thePlayer = player.gameObject;
        }

        if (mainCam == null)
        {
            var cameraObject = Camera.main;
            if (cameraObject != null)
                mainCam = cameraObject.gameObject;
        }

        if (collisionFX == null)
            collisionFX = FindObjectOfType<AudioSource>(true);

        if (thePlayer != null)
            playerMovement = thePlayer.GetComponent<PlayerMovement>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (isProcessing || Time.time < reviveInvulnerabilityUntil)
            return;

        if (thePlayer == null || playerMovement == null)
            return;

        if (!ShouldTriggerCrash(other))
            return;

        isProcessing = true;
        playerMovement.enabled = false;

        var director = RunGameplayDirector.EnsureExists();
        if (director != null && director.TryHandleRevive(this, other))
            return;

        BeginDeathSequence();
    }

    bool ShouldTriggerCrash(Collider other)
    {
        if (other == null || other.transform.root != thePlayer.transform)
            return false;

        Vector3 playerPosition = thePlayer.transform.position;
        Vector3 closestPoint = other.ClosestPoint(playerPosition);
        Vector3 localPoint = thePlayer.transform.InverseTransformPoint(closestPoint);

        bool obstacleAhead = closestPoint.z >= playerPosition.z - 0.2f;
        bool centerHit = Mathf.Abs(localPoint.x) <= 0.62f;
        bool nearBody = Mathf.Abs(closestPoint.y - playerPosition.y) < 1.6f;

        return obstacleAhead && centerHit && nearBody;
    }

    public void BeginDeathSequence()
    {
        StartCoroutine(CollisionEnd());
    }

    public void ReviveAfterSlot(Collider obstacle)
    {
        Vector3 position = thePlayer.transform.position;

        if (obstacle != null)
        {
            float pushDirection = Mathf.Sign(position.x - obstacle.bounds.center.x);
            if (Mathf.Approximately(pushDirection, 0f))
                pushDirection = Random.value > 0.5f ? 1f : -1f;

            position.x += pushDirection * 1.35f;
            position.z -= 2.2f;
        }

        position.x = Mathf.Clamp(position.x, -3.2f, 3.2f);
        thePlayer.transform.position = position;
        reviveInvulnerabilityUntil = Time.time + 1.35f;
        isProcessing = false;
        playerMovement.enabled = true;
    }

    IEnumerator CollisionEnd()
    {
        if (collisionFX != null)
            collisionFX.Play();

        if (mainCam != null)
        {
            var animator = mainCam.GetComponent<Animator>();
            if (animator != null)
                animator.Play("CollisionCam");
        }

        if (fadeOut != null)
            fadeOut.SetActive(true);

        yield return new WaitForSeconds(1.05f);
        SceneManager.LoadScene(0);
    }
}
