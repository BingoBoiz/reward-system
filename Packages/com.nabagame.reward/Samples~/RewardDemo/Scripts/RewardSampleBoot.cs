using NabaGame.Reward;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    public enum SampleStartPanel
    {
        None,
        DailyReward,
        LuckySpin
    }

    // the sample's composition root - a real game would do this from GameController.StartClass()
    public class RewardSampleBoot : MonoBehaviour
    {
        [SerializeField] DailyRewardManager dailyRewardManager;
        [SerializeField] DailyRewardRowData dailyRewardConfig;
        [SerializeField] LuckySpinManager luckySpinManager;
        [SerializeField] LuckySpinRowData luckySpinConfig;
        [SerializeField] RewardItemData rewardItemData;
        [SerializeField] SampleCurrencyHud currencyHud;
        [SerializeField] SampleHomeButtons homeButtons;
        [SerializeField] AudioSource sfxSource;
        [SerializeField] SampleStartPanel startPanel = SampleStartPanel.DailyReward;

        RewardHooks hooks;

        void OnValidate()
        {
            if (!dailyRewardManager) dailyRewardManager = GetComponentInChildren<DailyRewardManager>(true);
            if (!luckySpinManager) luckySpinManager = GetComponentInChildren<LuckySpinManager>(true);
            if (!sfxSource) sfxSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            StartClass();
        }

        public void StartClass()
        {
            hooks = new RewardHooks
            {
                Granter = new SampleRewardGranter(),
                Catalog = rewardItemData,
                PlaySfx = PlaySfx,
                ShowRewardedAd = ShowRewardedAd
            };

            dailyRewardManager.StartClass(dailyRewardConfig, hooks);
            luckySpinManager.StartClass(luckySpinConfig, hooks);
            currencyHud.StartClass();
            SampleUIRoot.Instance.StartClass();
            SampleUIRoot.Instance.dailyRewardPanel.StartClass(dailyRewardManager, hooks);
            SampleUIRoot.Instance.luckySpinPanel.StartClass(luckySpinManager, hooks);
            homeButtons.StartClass(dailyRewardManager, luckySpinManager);

            switch (startPanel)
            {
                case SampleStartPanel.None:
                    break;
                case SampleStartPanel.DailyReward:
                    SampleUIRoot.Instance.dailyRewardPanel.OpenPanel();
                    break;
                case SampleStartPanel.LuckySpin:
                    SampleUIRoot.Instance.luckySpinPanel.OpenPanel();
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(startPanel), startPanel, null);
            }
        }

        void PlaySfx(AudioClip clip)
        {
            if (clip && sfxSource) sfxSource.PlayOneShot(clip);
        }

        void ShowRewardedAd(string placement, System.Action onReward, System.Action onSkip)
        {
            Debug.Log($"[RewardSampleBoot] no ads SDK in the sample, '{placement}' rewards immediately");
            onReward();
        }

        [Button, DisableInEditorMode]
        void PreviewResetEverything()
        {
            SampleRewardGranter.ResetAll();
            dailyRewardManager.ResetProfile();
            luckySpinManager.ResetProfile();
            currencyHud.StartClass();
        }
    }
}
