using UnityEngine;
using TMPro;
using System;

public class MasterLevelInfo : MonoBehaviour
{
    public static event Action<int> CoinCountChanged;

    static int coinCount;
    static int coinMultiplier = 1;

    public static int CoinCount
    {
        get => coinCount;
        private set
        {
            if (coinCount == value)
                return;

            coinCount = value;
            CoinCountChanged?.Invoke(coinCount);
        }
    }

    [SerializeField] TMP_Text coinDisplay;

    void OnEnable()
    {
        CoinCountChanged += UpdateDisplay;
        UpdateDisplay(CoinCount);
    }

    void OnDisable()
    {
        CoinCountChanged -= UpdateDisplay;
    }

    public static void ResetCoins()
    {
        CoinCount = 0;
    }

    public static void SetCoinMultiplier(int multiplier)
    {
        coinMultiplier = Mathf.Max(1, multiplier);
    }

    public static void AddCoin(int amount = 1)
    {
        CoinCount += amount * coinMultiplier;
    }

    void UpdateDisplay(int value)
    {
        if (coinDisplay == null)
            return;

        coinDisplay.enableAutoSizing = true;
        coinDisplay.fontSizeMin = 20f;
        coinDisplay.fontSizeMax = 40f;
        coinDisplay.text = $"Coins: {value:00}";
    }
}
