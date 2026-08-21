using System;
using Cysharp.Threading.Tasks;
using NabaGame.Reward;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    // composition root: assigns the RewardHooks statics, then feeds each panel through its manager.
    // everything runs from Start() — panels apply startHidden in their own Start(), so nothing opens here
    public class SampleRewardBoot : MonoBehaviour
    {
        // Swallow = the shipped SDK's reward-interval throttle: neither callback ever fires
        public enum AdResult
        {
            Reward,
            Skip,
            Swallow
        }

        [SerializeField] SampleDailyRewardManager dailyRewardManager;
        [SerializeField] SampleLuckySpinManager luckySpinManager;
        [SerializeField] SampleOnlineRewardManager onlineRewardManager;
        [SerializeField] SampleCurrencyHud currencyHud;
        [SerializeField] SampleHomeButtons homeButtons;
        [SerializeField] AudioSource sfxSource;
        [SerializeField] bool failIapPurchase;
        [SerializeField] AdResult adResult = AdResult.Reward;
        [SerializeField] string fakeIapPrice = "";

        void OnValidate()
        {
            if (!dailyRewardManager) dailyRewardManager = GetComponentInChildren<SampleDailyRewardManager>(true);
            if (!luckySpinManager) luckySpinManager = GetComponentInChildren<SampleLuckySpinManager>(true);
            if (!onlineRewardManager) onlineRewardManager = GetComponentInChildren<SampleOnlineRewardManager>(true);
            if (!sfxSource) sfxSource = GetComponent<AudioSource>();
        }

        void Start()
        {
            SetInfo();
        }

        public void SetInfo()
        {
            // hooks first: panels may play sfx or show ads from the very first refresh
            RewardHooks.PlaySfx = PlaySfx;
            RewardHooks.ShowRewardedAd = (placement, onReward, onSkip) => ShowRewardedAd(placement, onReward, onSkip).Forget();
            RewardHooks.PurchaseIap = PurchaseIap;
            RewardHooks.GetIapPrice = GetIapPrice;
            RewardHooks.ShowMessage = ShowMessage;

            SampleUIRoot.Instance.SetInfo();
            dailyRewardManager.SetInfo();
            luckySpinManager.SetInfo();
            onlineRewardManager.SetInfo();

            currencyHud.SetInfo();
            SampleUIRoot.Instance.itemReceivedPanel.SetInfo();
            homeButtons.SetInfo();
        }

        void PlaySfx(AudioClip clip)
        {
            if (clip && sfxSource) sfxSource.PlayOneShot(clip);
        }

        // async on purpose: the shipped SDK never answers on the same frame
        async UniTaskVoid ShowRewardedAd(string placement, Action onReward, Action onSkip)
        {
            Debug.Log($"[SampleRewardBoot] fake ad '{placement}' -> {adResult}");
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), DelayType.Realtime);

            if (adResult == AdResult.Reward) onReward();
            else if (adResult == AdResult.Skip) onSkip();
        }

        string GetIapPrice(string productId) => fakeIapPrice;

        void ShowMessage(string message)
        {
            Debug.Log($"<color=orange>[SampleRewardBoot] toast: {message}</color>");
        }

        void PurchaseIap(string productId, Action<bool> callback)
        {
            Debug.Log($"[SampleRewardBoot] no IAP SDK in the sample, '{productId}' -> {!failIapPurchase}");
            callback(!failIapPurchase);
        }

        [Button, DisableInEditorMode]
        void PreviewResetEverything()
        {
            SampleRewardGranter.ResetAll();
            SampleUIRoot.Instance.dailyRewardPanel.ResetProfile();
            SampleUIRoot.Instance.luckySpinPanel.ResetProfile();
            SampleUIRoot.Instance.onlineRewardPanel.ResetSession();
            currencyHud.SetInfo();
        }
    }
}
