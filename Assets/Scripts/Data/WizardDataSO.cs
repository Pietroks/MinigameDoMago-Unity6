using UnityEngine;

namespace WizardGame.Data
{
    /// <summary>
    /// Definicao de dados desacoplados (ScriptableObject) para os tipos de goblins.
    /// Contem atributos de gameplay e colecoes de sprites para o FrameAnimator.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGoblinData", menuName = "WizardGame/Goblin Data")]
    public class WizardDataSO : ScriptableObject
    {
        [Header("Identificacao e Visual")]
        public WizardType wizardType;
        public string displayName = "Goblin";
        public Sprite sprite; // Sprite padrao / thumbnail
        public Sprite portraitSprite; // Retrato de alta qualidade para UI/Instrucoes
        public Color baseTint = Color.white;

        [Header("Animacoes por Frames")]
        public Sprite[] idleFrames;
        public Sprite[] walkFrames;
        public Sprite[] runFrames;
        public Sprite[] attackFrames;
        public Sprite[] attack2Frames;
        public Sprite[] attack3Frames;
        public Sprite[] summonFrames;
        public Sprite[] hitFrames;
        public Sprite[] teleportFrames;
        public Sprite[] deathFrames;
        public Sprite specialActionSprite; // Jump (Fugitivo), Shield (Dourado), Portal (Fantasma)

        [Header("Atributos de Gameplay")]
        [Tooltip("Quantidade de dano necessaria para abater o goblin.")]
        public int maxHealth = 1;

        [Tooltip("Pontos concedidos ao jogador ao abater este goblin.")]
        public int pointsOnDefeat = 1;

        [Tooltip("Velocidade base de deslocamento.")]
        public float moveSpeed = 2.0f;

        [Tooltip("Tempo em segundos ate que o goblin fuja do cenario.")]
        public float escapeTimeSeconds = 5.0f;

        [Range(1, 100)]
        [Tooltip("Peso relativo no sorteio de spawn ponderado.")]
        public int spawnWeight = 50;

        [Tooltip("Penalidade somada ao total de fugitivos quando este goblin foge.")]
        public int escapePenalty = 1;

        [Header("Efeitos Sonoros Especificos")]
        [Tooltip("Audio reproduzido ao escapar.")]
        public AudioClip escapeSound;

        [Tooltip("Audio especial reproduzido ao sofrer dano.")]
        public AudioClip customDamageSound;

        [Tooltip("Audio especial de morte.")]
        public AudioClip customDeathSound;

        [Header("Inteligência Artificial & Personalidade")]
        [Tooltip("Arquétipo comportamental que define o estilo e personalidade da IA.")]
        public AIPersonalityArchetype personalityArchetype = AIPersonalityArchetype.CommonWanderer;

        [Tooltip("Padrão primário de locomoção.")]
        public AIMovementPattern movementPattern = AIMovementPattern.Wander;

        [Tooltip("Reação disparada imediatamente ao receber dano.")]
        public AIHitReaction hitReaction = AIHitReaction.Jiggle;

        [Tooltip("Efeito de retaliação disparado na derrota.")]
        public AIDeathReaction deathReaction = AIDeathReaction.NormalFade;

        [Header("Parâmetros de Personalidade")]
        [Tooltip("Multiplicador de velocidade durante a fuga (FleeAfterHit).")]
        public float fleeSpeedMultiplier = 2.2f;

        [Tooltip("Duração do estado de disparada e pânico em segundos.")]
        public float fleeDuration = 2.5f;

        [Tooltip("Raio de ação para magos de suporte (Cura/Proteção).")]
        public float supportRadius = 3.5f;

        /// <summary>
        /// Aplica os comportamentos padrão de acordo com o arquétipo de personalidade escolhido.
        /// </summary>
        public void ApplyArchetypeDefaults()
        {
            switch (personalityArchetype)
            {
                case AIPersonalityArchetype.CommonWanderer:
                    movementPattern = AIMovementPattern.Wander;
                    hitReaction = AIHitReaction.Jiggle;
                    deathReaction = AIDeathReaction.NormalFade;
                    break;
                case AIPersonalityArchetype.FastFleeAfterHit:
                    movementPattern = AIMovementPattern.Wander;
                    hitReaction = AIHitReaction.FleeAfterHit;
                    deathReaction = AIDeathReaction.NormalFade;
                    break;
                case AIPersonalityArchetype.GoldenStraight:
                    movementPattern = AIMovementPattern.StraightMovement;
                    hitReaction = AIHitReaction.DefensiveShield;
                    deathReaction = AIDeathReaction.NormalFade;
                    break;
                case AIPersonalityArchetype.GhostTeleporter:
                    movementPattern = AIMovementPattern.SpectralHover;
                    hitReaction = AIHitReaction.TeleportAfterHit;
                    deathReaction = AIDeathReaction.NormalFade;
                    break;
                case AIPersonalityArchetype.ShieldProtector:
                    movementPattern = AIMovementPattern.ProtectAlly;
                    hitReaction = AIHitReaction.DefensiveShield;
                    deathReaction = AIDeathReaction.NormalFade;
                    break;
                case AIPersonalityArchetype.HealerSupport:
                    movementPattern = AIMovementPattern.HealerSupport;
                    hitReaction = AIHitReaction.FleeAfterHit;
                    deathReaction = AIDeathReaction.NormalFade;
                    break;
                case AIPersonalityArchetype.Illusionist:
                    movementPattern = AIMovementPattern.Wander;
                    hitReaction = AIHitReaction.SpawnDecoy;
                    deathReaction = AIDeathReaction.NormalFade;
                    break;
                case AIPersonalityArchetype.BomberKamikaze:
                    movementPattern = AIMovementPattern.StraightMovement;
                    hitReaction = AIHitReaction.Jiggle;
                    deathReaction = AIDeathReaction.ExplodeOnDeath;
                    break;
                case AIPersonalityArchetype.BossTactical:
                    movementPattern = AIMovementPattern.BossTactical;
                    hitReaction = AIHitReaction.Jiggle;
                    deathReaction = AIDeathReaction.NormalFade;
                    break;
            }
        }

        private void OnValidate()
        {
            // Aplica sincronização se os padrões estiverem em branco ou atualizados
            if (personalityArchetype != AIPersonalityArchetype.CommonWanderer && movementPattern == AIMovementPattern.Wander && hitReaction == AIHitReaction.Jiggle)
            {
                ApplyArchetypeDefaults();
            }
        }
    }
}