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
    }
}