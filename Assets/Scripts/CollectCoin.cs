using UnityEngine;

public class CollectCoin : MonoBehaviour
{
    bool collected;

    void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryCollect(other);
    }

    void TryCollect(Collider other)
    {
        if (collected)
            return;

        var player = FindObjectOfType<PlayerMovement>();
        if (player == null)
            return;

        if (other.transform.root != player.transform)
            return;

        collected = true;
        MasterLevelInfo.AddCoin();
        RuntimeAudioDirector.PlayCoinCollect();
        this.gameObject.SetActive(false);
    }

    public void PrepareForReuse()
    {
        collected = false;
        gameObject.SetActive(true);
    }
}
