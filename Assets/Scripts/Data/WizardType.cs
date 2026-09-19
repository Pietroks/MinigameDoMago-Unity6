namespace WizardGame.Data
{
    /// <summary>
    /// Tipos de goblins disponiveis no jogo, correspondentes as variantes oficiais.
    /// </summary>
    public enum WizardType
    {
        Comum,
        Fugitivo,
        Dourado,
        Fantasma,
        Chefe,
        Chefe2,
        Chefe3,
        Chefe4,
        ChefeFinal,
        Escudo,       // ShieldWizard (Protect)
        Curandeiro,   // HealerWizard (HealNearby)
        Ilusionista,  // IllusionWizard (CreateDecoys)
        Bombardeiro   // BomberWizard (ExplodeOnDeath)
    }
}