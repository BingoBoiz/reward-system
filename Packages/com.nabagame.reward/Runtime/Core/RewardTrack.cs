namespace NabaGame.Reward
{
    // param keys the package sends. the event name is the string authored on each panel, so the shape
    // analytics dashboards read is: event = feature group, param key = action, param value = the detail
    public static class RewardTrack
    {
        public const string Open = "open";
        public const string Claim = "claim";
        public const string Spin = "spin";
        public const string SpeedUp = "speed_up";
        public const string OpenAll = "open_all";
        public const string AdsStart = "ads_start";
        public const string AdsDone = "ads_done";
        public const string AdsFail = "ads_fail";
        public const string IapStart = "iap_start";
        public const string IapDone = "iap_done";
        public const string IapFail = "iap_fail";

        // shared guard: a panel left with an empty event name opts the whole feature out of analytics
        public static void Send(string eventName, string paramName, string paramValue)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            RewardHooks.TrackEvent(eventName, paramName, paramValue);
        }

        // analytics services drop names they cannot parse without telling anyone, so panels check theirs
        // at SetInfo. ascii only on purpose: char.IsLetter would wave through accented names that get dropped
        public static bool IsValidEventName(string eventName)
        {
            if (string.IsNullOrEmpty(eventName) || eventName.Length > 40) return false;
            if (!IsAsciiLetter(eventName[0])) return false;

            foreach (char c in eventName)
                if (c != '_' && !IsAsciiLetter(c) && (c < '0' || c > '9'))
                    return false;

            return true;
        }

        static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
