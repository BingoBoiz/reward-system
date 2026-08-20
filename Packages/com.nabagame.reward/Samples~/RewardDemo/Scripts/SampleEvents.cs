using NabaGame.Core.Runtime.EventManager;
using UnityEngine;

namespace NabaGame.Reward.Sample
{
    public class SampleCurrencyChangedEvent : GameEvent
    {
    }

    public class SampleItemGrantedEvent : GameEvent
    {
        public string Key;
        public Sprite Icon;
        public long Amount;
    }
}
