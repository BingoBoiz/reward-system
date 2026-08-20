using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NabaGame.Reward
{
    public class LuckySpinWedge : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text amountLabel;

        Tween punchTween;

        public int Index { get; private set; }

        public void SetInfo(int index, LuckySpinRow row)
        {
            Index = index;
            if (icon) icon.sprite = row.Icon;
            if (amountLabel) amountLabel.text = "+" + RewardAmountFormat.Short(row.Amount);
        }

        public void KeepUpright(float wheelZ)
        {
            transform.localEulerAngles = new Vector3(0f, 0f, -wheelZ);
        }

        public void PlayWinPunch()
        {
            punchTween?.Kill();
            transform.localScale = Vector3.one;
            punchTween = transform.DOPunchScale(Vector3.one * 0.35f, 0.5f, 6, 0.7f).SetUpdate(true).SetLink(gameObject);
        }

        void OnDisable()
        {
            punchTween?.Kill();
            punchTween = null;
            transform.localScale = Vector3.one;
        }
    }
}
