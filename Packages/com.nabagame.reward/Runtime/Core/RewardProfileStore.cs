using System;
using UnityEngine;

namespace NabaGame.Reward
{
    [Serializable]
    public abstract class RewardProfile
    {
        public int Version;
    }

    public static class RewardProfileStore
    {
        public static T Load<T>(string key, int expectedVersion) where T : RewardProfile, new()
        {
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(json)) return new T { Version = expectedVersion };

            T profile = JsonUtility.FromJson<T>(json);
            if (profile == null)
            {
                Debug.LogError($"[RewardProfileStore] '{key}' holds unreadable json, resetting: {json}");
                return new T { Version = expectedVersion };
            }

            if (profile.Version == expectedVersion) return profile;

            Debug.LogError($"[RewardProfileStore] '{key}' is version {profile.Version}, expected {expectedVersion}. No migration exists, resetting.");
            return new T { Version = expectedVersion };
        }

        public static void Save<T>(string key, T profile) where T : RewardProfile
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static void Clear(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
