using NabaGame.Core.Runtime.EventManager;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    // stands in for the game's economy: each row's OnClaimed callback lands here,
    // and the host is the only side that knows what "cash" means
    public static class SampleRewardGranter
    {
        public const string CashKey = "Sample.Cash";
        public const string SpinKey = "Sample.Spin";
        public const string NoAdsKey = "Sample.NoAds";

        public static long GetAmount(string prefsKey) => long.Parse(PlayerPrefs.GetString(prefsKey, "0"));

        public static bool HasNoAds => PlayerPrefs.GetInt(NoAdsKey, 0) == 1;

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(CashKey);
            PlayerPrefs.DeleteKey(SpinKey);
            PlayerPrefs.DeleteKey(NoAdsKey);
            PlayerPrefs.Save();
            EventManager.Instance.Raise(new SampleCurrencyChangedEvent());
        }

        public static void Grant(string key, Sprite icon, long amount)
        {
            switch (key)
            {
                case "cash":
                    Add(CashKey, amount);
                    break;

                case "spin":
                    Add(SpinKey, amount);
                    break;

                case "noads":
                    PlayerPrefs.SetInt(NoAdsKey, 1);
                    break;

                default:
                    Debug.LogError($"[SampleRewardGranter] reward key '{key}' x{amount} has no mapping in the sample host");
                    return;
            }

            PlayerPrefs.Save();
            EventManager.Instance.Raise(new SampleCurrencyChangedEvent());
            EventManager.Instance.Raise(new SampleItemGrantedEvent { Key = key, Icon = icon, Amount = amount });
        }

        static void Add(string prefsKey, long amount)
        {
            PlayerPrefs.SetString(prefsKey, (GetAmount(prefsKey) + amount).ToString());
        }
    }
}
