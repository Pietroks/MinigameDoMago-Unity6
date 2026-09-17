using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGame.Data
{
    [Serializable]
    public class WaveEnemyEntry
    {
        public WizardType wizardType;
        public int count = 1;

        public WaveEnemyEntry() { }

        public WaveEnemyEntry(WizardType type, int count)
        {
            this.wizardType = type;
            this.count = count;
        }
    }

    [Serializable]
    public class WaveConfig
    {
        public string waveName = "Onda 1";
        public string waveDescription = "Invasão Inicial de Goblins";
        public float spawnInterval = 1.4f;
        public float speedMultiplier = 1.0f;
        public List<WaveEnemyEntry> enemies = new List<WaveEnemyEntry>();

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                if (enemies != null)
                {
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        if (enemies[i] != null) total += enemies[i].count;
                    }
                }
                return total;
            }
        }

        public WaveConfig() { }

        public WaveConfig(string name, string desc, float interval, float speedMult, params (WizardType type, int count)[] entries)
        {
            this.waveName = name;
            this.waveDescription = desc;
            this.spawnInterval = interval;
            this.speedMultiplier = speedMult;
            this.enemies = new List<WaveEnemyEntry>();

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    this.enemies.Add(new WaveEnemyEntry(entry.type, entry.count));
                }
            }
        }

        /// <summary>
        /// Gera a lista sequencial embaralhada de tipos de inimigos para esta onda.
        /// </summary>
        public List<WizardType> GenerateShuffledEnemyList()
        {
            List<WizardType> list = new List<WizardType>();
            if (enemies != null)
            {
                foreach (var entry in enemies)
                {
                    for (int i = 0; i < entry.count; i++)
                    {
                        list.Add(entry.wizardType);
                    }
                }
            }

            // Fisher-Yates Shuffle para variação orgânica no spawn
            for (int i = list.Count - 1; i > 0; i--)
            {
                int rnd = UnityEngine.Random.Range(0, i + 1);
                WizardType temp = list[i];
                list[i] = list[rnd];
                list[rnd] = temp;
            }

            return list;
        }
    }
}
