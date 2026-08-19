using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace NabaGame.Reward
{
    // public fields in sheet column order; the importer fills them by index (original) or by name (Feeder)
    [Serializable]
    public class LuckySpinRow
    {
        public int Wedge;
        public string Key;
        public long Amount;
        public int Weight;
    }

    // importer-shaped: sheet tab 'LuckySpin' (A1 = LuckySpinRow) fills luckySpinRows; the other fields survive re-import
    [CreateAssetMenu(menuName = "NabaGame/Reward/Lucky Spin Data", fileName = "RawLuckySpin")]
    public class LuckySpinRowData : ScriptableObject
    {
        // clockwise from 12 o'clock; row count must match the wheel art's segment count
        [TableList] public List<LuckySpinRow> luckySpinRows = new List<LuckySpinRow>();
        [Min(1)] public int FreeSpinCooldownSeconds = 1800;
        [Min(1f)] public float SpinDurationSeconds = 4.5f;
        [Min(1)] public int SpinFullTurns = 5;

        public int WedgeCount => luckySpinRows.Count;

        public LuckySpinRow GetRow(int index) => luckySpinRows[index];

        public int Roll()
        {
            int total = 0;
            for (int i = 0; i < luckySpinRows.Count; i++) total += luckySpinRows[i].Weight;

            int pick = UnityEngine.Random.Range(0, total);
            for (int i = 0; i < luckySpinRows.Count; i++)
            {
                pick -= luckySpinRows[i].Weight;
                if (pick < 0) return i;
            }

            throw new InvalidOperationException($"[LuckySpinRowData] '{name}' roll fell through, total weight {total}");
        }

        public void ValidateOrThrow()
        {
            if (luckySpinRows.Count < 2)
                throw new InvalidOperationException($"[LuckySpinRowData] '{name}' has {luckySpinRows.Count} wedges, expected at least 2");

            for (int i = 0; i < luckySpinRows.Count; i++)
            {
                LuckySpinRow row = luckySpinRows[i];
                if (row.Wedge != i + 1)
                    throw new InvalidOperationException($"[LuckySpinRowData] '{name}' row {i} has Wedge {row.Wedge}, expected {i + 1}");
                if (string.IsNullOrEmpty(row.Key))
                    throw new InvalidOperationException($"[LuckySpinRowData] '{name}' wedge {row.Wedge} has an empty Key");
                if (row.Amount <= 0)
                    throw new InvalidOperationException($"[LuckySpinRowData] '{name}' wedge {row.Wedge} has Amount {row.Amount}");
                if (row.Weight <= 0)
                    throw new InvalidOperationException($"[LuckySpinRowData] '{name}' wedge {row.Wedge} has Weight {row.Weight}, expected > 0");
            }
        }

        void OnValidate()
        {
            try
            {
                ValidateOrThrow();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(e.Message, this);
            }
        }
    }
}
