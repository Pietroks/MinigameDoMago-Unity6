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
        [Header("Identidade da Fase & Chefe")]
        public int stageNumber = 1;
        public string waveName = "Muralhas da Fortaleza";
        public string waveDescription = "Invasão Inicial de Goblins";
        public string bossDisplayName = "Grande Goblin";
        public Sprite backgroundSprite;
        public Color backgroundTint = Color.white;
        public float backgroundZoom = 1.0f;
        public Color ambientParticleColor = new Color(0.6f, 0.85f, 1f, 0.55f);
        public AudioClip defaultMusicClip;

        [Header("Ritmo e Progressão de Dificuldade")]
        public float spawnInterval = 1.35f;
        public float speedMultiplier = 1.0f;
        public float fugitiveSpeedMultiplier = 1.0f;
        public float escapeTimerMultiplier = 1.0f;
        public int maxConcurrentEnemies = 4;
        [Range(0f, 1f)] public float doubleSpawnChance = 0f;

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
            : this(1, name, desc, "Chefe", null, Color.white, 1.0f, new Color(0.6f, 0.85f, 1f, 0.55f), interval, speedMult, 1.0f, 5, 0f, entries)
        {
        }

        public WaveConfig(
            int stageNumber,
            string name,
            string desc,
            string bossDisplayName,
            Sprite backgroundSprite,
            Color backgroundTint,
            float backgroundZoom,
            Color ambientParticleColor,
            float interval,
            float speedMult,
            float escapeTimerMult,
            int maxConcurrent,
            float doubleSpawnChance,
            params (WizardType type, int count)[] entries)
            : this(stageNumber, name, desc, bossDisplayName, backgroundSprite, backgroundTint, backgroundZoom, ambientParticleColor, null, interval, speedMult, speedMult, escapeTimerMult, maxConcurrent, doubleSpawnChance, entries)
        {
        }

        public WaveConfig(
            int stageNumber,
            string name,
            string desc,
            string bossDisplayName,
            Sprite backgroundSprite,
            Color backgroundTint,
            float backgroundZoom,
            Color ambientParticleColor,
            AudioClip defaultMusicClip,
            float interval,
            float speedMult,
            float fugitiveSpeedMult,
            float escapeTimerMult,
            int maxConcurrent,
            float doubleSpawnChance,
            params (WizardType type, int count)[] entries)
        {
            this.stageNumber = stageNumber;
            this.waveName = name;
            this.waveDescription = desc;
            this.bossDisplayName = bossDisplayName;
            this.backgroundSprite = backgroundSprite;
            this.backgroundTint = backgroundTint;
            this.backgroundZoom = backgroundZoom;
            this.ambientParticleColor = ambientParticleColor;
            this.defaultMusicClip = defaultMusicClip;
            this.spawnInterval = interval;
            this.speedMultiplier = speedMult;
            this.fugitiveSpeedMultiplier = fugitiveSpeedMult;
            this.escapeTimerMultiplier = escapeTimerMult;
            this.maxConcurrentEnemies = maxConcurrent;
            this.doubleSpawnChance = doubleSpawnChance;
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
        /// Verifica se o tipo de mago/goblin é um chefe.
        /// </summary>
        public static bool IsBossType(WizardType type)
        {
            return type == WizardType.Chefe ||
                   type == WizardType.Chefe2 ||
                   type == WizardType.Chefe3 ||
                   type == WizardType.Chefe4 ||
                   type == WizardType.ChefeFinal;
        }

        /// <summary>
        /// Gera a lista sequencial de tipos de inimigos para esta onda.
        /// Minions comuns são embaralhados aleatoriamente, enquanto Chefes são posicionados
        /// estritamente no final da onda (ex: no 10º goblin) como clímax da batalha.
        /// </summary>
        public List<WizardType> GenerateShuffledEnemyList()
        {
            List<WizardType> minions = new List<WizardType>();
            List<WizardType> bosses = new List<WizardType>();

            if (enemies != null)
            {
                foreach (var entry in enemies)
                {
                    for (int i = 0; i < entry.count; i++)
                    {
                        if (IsBossType(entry.wizardType))
                        {
                            bosses.Add(entry.wizardType);
                        }
                        else
                        {
                            minions.Add(entry.wizardType);
                        }
                    }
                }
            }

            // Fisher-Yates Shuffle para variação orgânica nos lacaios
            for (int i = minions.Count - 1; i > 0; i--)
            {
                int rnd = UnityEngine.Random.Range(0, i + 1);
                WizardType temp = minions[i];
                minions[i] = minions[rnd];
                minions[rnd] = temp;
            }

            // Chefes sempre entram no final da onda
            minions.AddRange(bosses);

            return minions;
        }
    }
}
