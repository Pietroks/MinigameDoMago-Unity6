using UnityEngine;

namespace WizardGame.Data
{
    /// <summary>
    /// Definição de dados desacoplados (ScriptableObject) para os tipos de magos.
    /// Permite balancear vida, pontuação, pesos de spawn e efeitos visuais/sonoros sem alterar código.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWizardData", menuName = "WizardGame/Wizard Data")]
    public class WizardDataSO : ScriptableObject
    {
        [Header("Identificação e Visual")]
        public WizardType wizardType;
        public string displayName = "Mago";
        public Sprite sprite;
        public Color baseTint = Color.white;

        [Header("Atributos de Gameplay")]
        [Tooltip("Quantidade de dano necessária para abater o mago.")]
        public int maxHealth = 1;

        [Tooltip("Pontos concedidos ao jogador ao abater este mago.")]
        public int pointsOnDefeat = 1;

        [Tooltip("Tempo em segundos até que o mago fuja do cenário.")]
        public float escapeTimeSeconds = 5.0f;

        [Range(1, 100)]
        [Tooltip("Peso relativo no sorteio de spawn ponderado.")]
        public int spawnWeight = 50;

        [Tooltip("Penalidade somada ao total de magos escapados quando este mago foge.")]
        public int escapePenalty = 1;

        [Header("Efeitos Sonoros Específicos")]
        [Tooltip("Áudio reproduzido ao escapar.")]
        public AudioClip escapeSound;

        [Tooltip("Áudio especial reproduzido ao sofrer dano (ex: DBZ teleport no fantasma ou Zé Wilker no rápido).")]
        public AudioClip customDamageSound;

        [Tooltip("Áudio especial de morte (ex: grito Peppino no dourado). Se nulo, toca áudio aleatório.")]
        public AudioClip customDeathSound;
    }
}
