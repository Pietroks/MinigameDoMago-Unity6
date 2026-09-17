namespace WizardGame.Data
{
    public enum SpellType
    {
        Normal,     // Tiro rápido da varinha (1 dano, consome mana)
        Arcane,     // Tiro Arcano pesado (3 dano direto, penetrante)
        Ice,        // Feitiço de Gelo (1 dano, congela e imobiliza por 2.5s)
        Lightning,  // Relâmpago em Cadeia (2 dano no alvo + 1 dano em até 2 próximos)
        Area        // Feitiço de Área / Supernova (2 dano radial em raio de 2.5m)
    }
}
