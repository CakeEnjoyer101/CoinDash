using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MasterLevelInfo : MonoBehaviour
{
    public static int coinCount = 0;
    [SerializeField] GameObject coinDisplay;

    void Update()
    {
        coinDisplay.GetComponent<TMPro.TMP_Text>().text = "Coins:" + coinCount;    
    }
}
