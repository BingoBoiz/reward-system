using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NabaGame.Reward
{
    // Shared wall-clock scheduler: one 1-second realtime loop serves every feature that
    // needs "run X at unix time T" (daily rollover, slot timers, spin cooldown).
    // Deadlines are re-checked against RewardClock.NowMs each tick, so app suspend/resume
    // needs no per-manager OnApplicationPause catch-up.
    public static class TimeScheduler
    {
        public class Handle
        {
            internal long atUnixMs;
            internal Action callback;
        }

        static readonly List<Handle> pending = new List<Handle>();
        static readonly List<Handle> due = new List<Handle>();
        static bool running;

        public static Handle Schedule(long atUnixMs, Action callback)
        {
            Handle handle = new Handle { atUnixMs = atUnixMs, callback = callback };
            pending.Add(handle);
            if (!running) Run().Forget();
            return handle;
        }

        public static void Cancel(ref Handle handle)
        {
            if (handle == null) return;
            pending.Remove(handle);
            handle = null;
        }

        static async UniTaskVoid Run()
        {
            running = true;
            while (pending.Count > 0 && Application.isPlaying)
            {
                await UniTask.Delay(1000, DelayType.Realtime);
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (RewardClock.NowMs < pending[i].atUnixMs) continue;
                    due.Add(pending[i]);
                    pending.RemoveAt(i);
                }

                // callbacks run after the sweep so they can Schedule/Cancel freely.
                // a throwing callback must not kill the shared loop for every other timer
                for (int i = 0; i < due.Count; i++)
                {
                    try
                    {
                        due[i].callback();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                due.Clear();
            }

            running = false;
        }

        // statics survive play sessions when domain reload is disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            pending.Clear();
            due.Clear();
            running = false;
        }
    }
}
