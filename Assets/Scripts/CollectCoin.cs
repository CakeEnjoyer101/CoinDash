using UnityEngine;

public class CollectCoin : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var player = FindObjectOfType<PlayerMovement>();
        if (player == null)
            return;

        if (other.transform.root != player.transform)
            return;

        MasterLevelInfo.AddCoin();
        this.gameObject.SetActive(false);
    }
}
