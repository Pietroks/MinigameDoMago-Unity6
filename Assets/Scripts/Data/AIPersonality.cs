namespace WizardGame.Data
{
    /// <summary>
    /// Arquétipos de personalidade para a Inteligência Artificial dos Magos/Goblins.
    /// Define o estilo comportamental e tático do inimigo no campo de batalha.
    /// </summary>
    public enum AIPersonalityArchetype
    {
        CommonWanderer,     // Wander: perambulação orgânica e ataques periódicos
        FastFleeAfterHit,   // Wander + FleeAfterHit: fuga em disparada frenética ao sofrer dano
        GoldenStraight,     // StraightMovement: patrulha retilínea militar e postura de escudo
        GhostTeleporter,    // SpectralHover + TeleportAfterHit: levitação suave e salto dimensional ao tomar dano
        ShieldProtector,    // Protect: move-se para cobrir e defender aliados feridos
        HealerSupport,      // HealNearby: recua e emite pulsos de regeneração para a horda
        Illusionist,        // CreateDecoys: invoca clones ilusórios para confundir o jogador
        BomberKamikaze,     // ExplodeOnDeath: detonação arcana explosiva de retaliação
        BossTactical        // BossTactical: padrões de fases e ataques pesados de chefes
    }

    /// <summary>
    /// Padrão primário de locomoção do inimigo.
    /// </summary>
    public enum AIMovementPattern
    {
        Wander,            // Perambulação orgânica errática com paradas e variações de rumo
        StraightMovement,  // Deslocamento contínuo em linha reta com reflexão precisa nas bordas
        SpectralHover,     // Flutuação etérea com ondulações senoidais e translucidez
        ProtectAlly,       // Deslocamento focado para a frente de aliados feridos
        HealerSupport,     // Posicionamento cauteloso mantendo distância do retículo
        BossTactical       // Movimentação orquestrada por máquina de estados de chefe
    }

    /// <summary>
    /// Reação imediata disparada no momento em que o inimigo recebe dano (sem ser derrotado).
    /// </summary>
    public enum AIHitReaction
    {
        Jiggle,            // Vibração física clássica e recuo leve
        FleeAfterHit,      // Pulo em choque e disparada acelerada em pânico para longe do disparo
        DefensiveShield,   // Postura defensiva erguendo escudo para absorver impacto
        TeleportAfterHit,  // Deslocamento dimensional imediato através de portal
        SpawnDecoy,        // Criação de cópias ilusórias de distração
        None               // Nenhuma reação especial (continua trajetória)
    }

    /// <summary>
    /// Efeito especial ou retaliação acionada no momento da derrota.
    /// </summary>
    public enum AIDeathReaction
    {
        NormalFade,        // Colapso clássico com fade out
        ExplodeOnDeath,    // Detonação explosiva com dano de área e tremor
        SpawnDecoysOnDeath // Deixa para trás cópias ilusórias como truque final
    }
}
