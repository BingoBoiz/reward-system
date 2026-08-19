using NabaGame.Core.Runtime.EventManager;
using NabaGame.Reward;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    // stands in for the game's economy: the package hands over a RewardItem.Key,
    // the host is the only side that knows what "cash" means
    public class SampleRewardGranter : IRewardGranter
    {
        public const string CashKey = "Sample.Cash";
        public const string SpinKey = "Sample.Spin";
        public const string NoAdsKey = "Sample.NoAds";

        static readonly SampleCurrencyChangedEvent changedEvent = new SampleCurrencyChangedEvent();

        public static long GetAmount(string prefsKey) => long.Parse(PlayerPrefs.GetString(prefsKey, "0"));

        public static bool HasNoAds => PlayerPrefs.GetInt(NoAdsKey, 0) == 1;

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(CashKey);
            PlayerPrefs.DeleteKey(SpinKey);
            PlayerPrefs.DeleteKey(NoAdsKey);
            PlayerPrefs.Save();
            EventManager.Instance.Raise(changedEvent);
        }

        public void Grant(RewardItem item, long amount)
        {
            switch (item.Key)
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
                    Debug.LogError($"[SampleRewardGranter] RewardItem key '{item.Key}' x{amount} has no mapping in the sample host");
                    return;
            }

            PlayerPrefs.Save();
            EventManager.Instance.Raise(changedEvent);
        }

        static void Add(string prefsKey, long amount)
        {
            PlayerPrefs.SetString(prefsKey, (GetAmount(prefsKey) + amount).ToString());
        }
    }
}
