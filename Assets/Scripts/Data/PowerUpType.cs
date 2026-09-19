namespace WizardGame.Data
{
    /// <summary>
    /// Variantes de power-ups especiais que surgem como orbes mágicos flutuantes raros.
    /// </summary>
    public enum PowerUpType
    {
        Mana,           // 🔵 Recupera 4 tiros imediatamente
        ArcaneFury,     // 🔥 Tiros causam +1 dano durante 5s
        ArcaneTime,     // ⏱️ Tudo fica 50% mais lento durante 3s
        Storm,          // 💥 Próximo tiro aniquila todos os magos comuns na tela
        Precision       // 🎯 Retículo de mira encolhe durante 5s para alta precisão
    }
}
