# Sheet template

Copy each block below (tab-separated) into cell **A1** of a Google Sheet tab with the given name, then run the NabaGame Googlesheet Importer → *Generate Assets* (never *Generate Script* — the classes live in this package). Content equals `Samples~/RewardDemo/Data/`. See INTEGRATION-GUIDE §7 for the importer settings.

Rules: first data row has no blank cells (`-` = no label override), bools are `TRUE`/`FALSE`, numbers are plain integers, `sp_Icon` is the sprite file name under the importer's `SpriteAssetFolder` (Feeder fork only; the original importer leaves sprites empty).

## Tab `RewardItem` → `RawRewardItem.asset` (`RewardItemData`)

```
RewardItem
s_Key	s_DisplayName	sp_Icon
cash	Cash	daily_0003_money
spin	Lucky Spin	daily_0001_spin
noads	No Ads	home_0002_no-ads
```

## Tab `DailyReward` → `RawDailyReward.asset` (`DailyRewardRowData`)

```
DailyRewardRow
n_Day	s_Key	l_Amount	s_LabelOverride	b_HideIconUntilClaim
1	cash	7500000	-	FALSE
2	spin	1	RARE	FALSE
3	spin	10	-	FALSE
4	cash	15000000	-	FALSE
5	cash	30000000	-	FALSE
6	cash	75000000	-	FALSE
7	noads	1	???	TRUE
```

## Tab `LuckySpin` → `RawLuckySpin.asset` (`LuckySpinRowData`)

```
LuckySpinRow
n_Wedge	s_Key	l_Amount	n_Weight
1	cash	400	24
2	spin	1	12
3	cash	800	18
4	cash	400	24
5	spin	2	6
6	cash	1500	10
7	cash	400	24
8	cash	5000	3
```
