namespace NabaGame.Reward
{
    public interface IRewardGranter
    {
        // must fail loudly on an unknown item.Key - a silent no-op ships as "claim gave nothing"
        void Grant(RewardItem item, long amount);
    }
}
