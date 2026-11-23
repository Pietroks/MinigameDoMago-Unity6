# Estado Atual do Projeto — Mata Goblin (Unity 6)

> **Última atualização:** 22/11/2026  
> **Título Oficial (`productName`):** `Mata Goblin`  
> **Engine / Pipeline:** Unity 6 (2D/2.5D, UGUI 2.0.0, Physics2D)  
> **Repositório:** `https://github.com/Pietroks/MinigameDoMago-Unity6.git` (branch `main`)  
> **Status da Compilação C#:** `Assembly-CSharp.csproj` e `Assembly-CSharp-Editor.csproj` compilando com **0 Erros e 0 Warnings** (`dotnet build`).

---

## 1. Visão Geral do Jogo

**Mata Goblin** é um jogo *point-and-click* arcade em primeira pessoa (POV de varinha mágica + retículo de mira) com temática *dark-fantasy* e cenários em **profundidade 2.5D** (parallax reativo ao mouse, obstáculos recortados onde os goblins passam por trás e pela frente, sombras de contato nos pés e partículas em 2 planos). O jogador defende o castelo ao longo de **5 fases progressivas**, enfrentando ondas balanceadas de goblins com personalidades de IA distintas e **5 chefes exclusivos**, gerenciando **Mana**, **Vida**, **Arsenal Mágico**, **Combos** e **Power-ups raros**.

### Condições de Vitória e Derrota
- **Vitória:** Derrotar o **Goblin Rei Supremo** ao final da **Fase 5**.
- **Derrota (Dupla Condição):**
  1. Deixar **15 goblins** escaparem da arena (`maxEscapesAllowed = 15`); **ou**
  2. Ter os **5 Pontos de Vida** (`maxPlayerHealth = 5`) zerados por ataques de chefes ou retaliações de fuga (`Fugitivo` e `Dourado`).

---

## 2. Arquitetura e Mapa de Scripts (`Assets/Scripts/`)

O projeto possui **21 scripts C#** organizados em 6 namespaces sob `WizardGame.*`:

| Pasta | Script | Responsabilidade Principal |
|---|---|---|
| `Core/` | `GameManager.cs` | Orquestrador da partida (`Menu`, `Playing`, `Paused`, `GameOver`), combate, combos, Headshot crítico, bônus de habilidade, rank composto (S–D), transições de fase e fluxo de áudio. |
| `Core/` | `SoundManager.cs` | Gerenciador de áudio 2D (BGM + 8 canais de SFX), crossfade em `unscaledDeltaTime`, mixagem calibrada por categoria, trilha padrão por fase (`stageDefaultTracks`), rotação automática de playlist, `StopMusic`, persistência em `PlayerPrefs` e evento `OnMuteChanged`. |
| `Core/` | `SpellEffectsManager.cs` | VFX procedurais sem dependências externas: **feixes mágicos da varinha ao alvo (`SpawnWandBeam`)**, gelo/estilhaços, relâmpago em cadeia (`LineRenderer`), explosão radial de área, *camera shake*, anéis de impacto e números flutuantes de dano/pontos (`SpawnFloatingText`). |
| `Core/` | `StageEnvironmentManager.cs` | Troca de cenários por fase, *color grading*, zoom atmosférico (`SetExtraZoom`) e partículas ambientais do bioma (poeira lunar, vaga-lumes, brasas vulcânicas). |
| `Core/` | `BackgroundScaler.cs` | *Aspect Fill* automático da câmera ortográfica em qualquer resolução/proporção, com detecção de redimensionamento em tempo real. |
| `Data/` | `WizardDataSO.cs` | `ScriptableObject` de configuração de cada inimigo (sprites, animações por quadros, HP, pontos, velocidade, tempo de fuga, sons e arquétipo de IA). |
| `Data/` | `WaveConfig.cs` | Configuração de cada uma das 5 fases (chefe, cenário, música padrão, `fugitiveSpeedMultiplier`, cadência de spawn, limite simultâneo, chance de spawn duplo e embaralhamento Fisher-Yates com chefe no clímax). |
| `Data/` | `AIPersonality.cs` | Enums modulares de personalidade (`AIPersonalityArchetype`, `AIMovementPattern`, `AIHitReaction`, `AIDeathReaction`). |
| `Data/` | `WizardType.cs` | Enum dos tipos ativos (`Comum`, `Fugitivo`, `Dourado`, `Fantasma`, `Chefe`..`ChefeFinal`) e extensões futuras (`Escudo`, `Curandeiro`, `Ilusionista`, `Bombardeiro`). |
| `Data/` | `SpellType.cs` | Enum do arsenal mágico (`Normal`, `Arcane`, `Ice`, `Lightning`, `Area`). |
| `Data/` | `PowerUpType.cs` | Enum dos 5 orbes raros (`Mana`, `ArcaneFury`, `ArcaneTime`, `Storm`, `Precision`). |
| `Entities/` | `WizardController.cs` | Ciclo de vida dos inimigos e chefes, profundidade 2.5D por eixo Y, barra de vida, alerta visual de fuga iminente (`!`), micro-stagger, reações de IA, ataques telegrafados e projéteis interceptáveis (`BossProjectileInstance`). |
| `Entities/` | `WizardSpawner.cs` | `ObjectPool<WizardController>` (Unity 6), fila de ondas, controle de superlotação, spawn duplo, reforços invocados por chefes e adaptação dinâmica de limites de tela. |
| `Entities/` | `FrameAnimator.cs` | Animador leve por spritesheets/frames (`Idle`, `Walk`, `Run`, `Attack`, `Attack2`, `Attack3`, `Summon`, `Hit`, `Death`, `Teleport`) com suporte a loop, one-shot e lock. |
| `Entities/` | `WeaponSystem.cs` | Varinha POV com *mouse sway*, respiração (*bobbing*), recuo (*recoil*), brilho elemental no cristal, recarga de Mana (10 cargas) em tempo real (`unscaledDeltaTime`) e feedback no retículo (hover, hitmarker, giro âmbar em recarga e brilho esmeralda em Precisão). |
| `Entities/` | `PowerUpManager.cs` | Spawn de orbes (14s–22s) com texto flutuante no mundo ao coletar, controle de buffs pausáveis com segurança em `Time.timeScale == 0` e Slow Motion seguro (`0.5x`). |
| `Entities/` | `PowerUpItem.cs` | Orbe flutuante coletável por disparo/clique com deriva horizontal, pulso luminoso e aviso de expiração. |
| `Input/` | `PlayerInputHandler.cs` | Captura de mira, disparos (`LMB`/`RMB`), seleção de feitiços (`1–4`, `Q/E/F/C`, `Scroll`), recarga (`R`), mute (`M`), pausa (`ESC`/`P`), detecção *Zero-GC* (`ContactFilter2D.noFilter`) e bloqueio contra cliques acidentais em botões do HUD. |
| `UI/` | `UIManager.cs` | Controle do Menu Inicial (com Recorde salvo), Configurações, Grimório, Pausa, HUD flutuante, **barra épica de Chefe no topo**, pulso de tensão com 1 de Vida, corações, cristais de mana, medalhão de feitiço, banners, cortina de transição de fase e tela de Rank. |
| `UI/` | `UIButtonJuice.cs` | Animação tátil em `unscaledDeltaTime` (hover `1.045x`, clique `0.96x` e brilho) para botões temáticos. |
| `Editor/` | `ProjectSetupWizard.cs` | Ferramenta de Editor (`Tools > Setup Mata Goblin Complete` e `Tools > Build Game Standalone`) que configura sprites (9-slice), áudios, `ScriptableObjects`, prefab `Goblin_Base` e reconstrói integralmente a cena `Assets/Scenes/Gameplay.unity`. |

---

## 3. Progressão das 5 Fases, Chefes e Trilha Sonora

Cada fase possui **cenário próprio**, **chefe dedicado**, **música padrão de entrada** (que rotaciona automaticamente para as demais faixas da playlist caso o jogador permaneça mais tempo na fase) e **velocidade progressiva do Goblin Fugitivo**. Ao derrotar o chefe, a música ambiente é encerrada (`StopMusic`) para tocar **apenas a música de vitória**, retornando somente quando a fase seguinte inicia.

| Fase | Cenário / Bioma + Obstáculos 2.5D | Chefe (HP / Pts) | Composição da Onda | Vel. Geral / Fugitivo | Música Padrão de Início |
|---|---|---|---|---|---|
| **1** | Muralhas da Fortaleza (`Prop_Stage1_Crates`) | **Grande Goblin** (18 HP / 50 pts) | 7 Comuns + 3 Fugitivos + Chefe | `0.98x` / **`0.58x`** | `Mago na Mira.mp3` |
| **2** | Ruínas Iluminadas pela Lua (`Prop_Stage2_Ruins`) | **Xamã do Caos** (24 HP / 80 pts) | 6 Comuns + 3 Fugitivos + 2 Dourados + Chefe | `1.05x` / **`0.68x`** | `Spooky Castle Comedy v2.mp3` |
| **3** | Fortaleza Vulcânica (`Prop_Stage3_Volcanic`) | **Feiticeiro Espectral** (32 HP / 120 pts) | 5 Comuns + 3 Fugitivos + 2 Dourados + 3 Fantasmas + Chefe | `1.12x` / **`0.80x`** | `Wizard_s Mishap.mp3` |
| **4** | Salão do Trono em Ruínas (`Prop_Stage4_ThronePillar`) | **Lorde Carmesim** (42 HP / 180 pts) | 4 Comuns + 4 Fugitivos + 3 Dourados + 3 Fantasmas + Chefe | `1.20x` / **`0.94x`** | `Sky Funeral.mp3` |
| **5** | Altar do Rei Supremo (`Prop_Stage4_ThronePillar`) | **Goblin Rei Supremo** (60 HP / 500 pts) | 3 Comuns + 4 Fugitivos + 4 Dourados + 4 Fantasmas + Chefe Final | `1.28x` / **`1.10x`** | `Arcane Confrontation.mp3` |

> **Faixa extra na rotação de playlist:** `Tavern Menu of Doom.mp3` (além da música dedicada de Menu: `Goblin Dungeon Menu.mp3`).  
> **Recompensa entre fases:** Bônus de pontos (`60 × fase`), recarga total da Mana e recuperação de **+2 Vida** (até o máximo de 5).

---

## 4. Sistemas de Combate, Inimigos e Power-ups

### Inimigos e Personalidades de IA
- **Goblin Comum** (`1 HP | 1 Pt | Wander`): perambula e faz ataques visuais periódicos; não causa dano à Vida ao escapar.
- **Goblin Fugitivo** (`2 HP | 2 Pts | Wander + FleeAfterHit`): salta e foge em disparada ao ser atingido; velocidade e arrancada crescem da Fase 1 até a Fase 5; causa **1 de Dano** ao jogador se escapar (`escapePenalty = 2`).
- **Goblin Dourado** (`3 HP | 5 Pts | StraightMovement + DefensiveShield`): patrulha retilínea com postura de escudo e brilho dourado; causa **2 de Dano** ao jogador se escapar.
- **Goblin Fantasma** (`4 HP | 3 Pts | SpectralHover + TeleportAfterHit`): flutuação translúcida e teleporte por portal ao tomar dano (tocando `dbz-teleport.mp3`).
- **Chefes (`BossTactical`)**: possuem **Fase 2 de Fúria aos 50% HP** (velocidade `+24%`, convocação de reforços), ataques pesados telegrafados interrompíveis (**+8 pts CONTRA-ATAQUE**) e projéteis 2.5D interceptáveis no ar (**+5 pts INTERCEPTAÇÃO**).

### Arsenal Mágico e Controles
- **`[LMB]` Tiro Normal:** 1 dano (consome 1 de 10 cargas de Mana; recarga rápida `0.78s`; **`+1` dano crítico em Headshot**).
- **`[RMB]` Feitiço Especial Selecionado:**
  - **`[1 / Q]` Tiro Arcano:** 4 dano direto (`CD: 2.1s`).
  - **`[2 / E]` Feitiço de Gelo:** 1 dano + congela por `2.8s` (`CD: 3.1s`).
  - **`[3 / F]` Relâmpago em Cadeia:** 2 dano no alvo principal + salta para até 3 alvos próximos em `3.8m` (`CD: 3.5s`).
  - **`[4 / C / Space]` Explosão em Área:** 3 dano radial em `2.8m` com onda de choque e tremor (`CD: 4.2s`).
- **Atalhos extras:** `Scroll` alterna feitiços, `[R]` recarrega Mana, `[M]` ativa/desativa Mute, `[ESC / P]` pausa/retoma o jogo (e `ESC` fecha painéis no menu).

### Combos, Bônus de Habilidade, Power-ups e Melhorias de Game Feel
- **Feixes Mágicos da Varinha (`SpawnWandBeam`):** Cada disparo normal ou especial dispara um feixe luminoso (núcleo branco + aura elemental) da ponta do cristal da varinha até o ponto clicado.
- **Barra Épica de Vida do Chefe no Topo do HUD:** Exibida automaticamente ao iniciar lutas contra chefes com animação suave de vida, nome do chefe e alerta pulsante `• FÚRIA!` abaixo de 50% de HP (sem sobrepor os indicadores de buffs ativos).
- **Multiplicadores de Combo & Cura por Habilidade:** `×1` (1–4), `×2` (5–9), `×3` (10–19), `×4` (20+ abates). Subir de patamar restaura **+2 Mana**, e atingir sequências de **12, 24, 36... abates** sem errar recupera **+1 Vida** (ou concede **+25 pts IMPARÁVEL!** se a vida estiver cheia).
- **Tensão em 1 HP & Feedback no Mundo:** Vinheta rubra pulsante suave quando o jogador cai para 1 ponto de Vida, além de textos flutuantes coloridos ao coletar Power-ups (`+4 MANA!`, `+1 DANO (5s)!`, etc.).
- **Persistência de Recorde (`PlayerPrefs`):** Salva automaticamente a maior pontuação e o melhor Rank (`MataGoblin_BestScore` / `MataGoblin_BestRank`), exibindo o recorde no Menu Inicial e destacando `• NOVO RECORDE! •` na tela final.
- **Bônus Dinâmicos:** Primeiro Tiro (`+5`), Headshot Crítico (`+2` e `+1` dano), Triplo Abate Rápido (`+5`), Interrupção Dimensional (`+10`), Desafio Dourado (`+3`), Feitiço Duplo (`+10`), Interceptação (`+5`), Contra-Ataque (`+8`), Tempestade (`+10`).
- **5 Power-ups Raros Flutuantes:** `Mana` (+4 tiros), `Fúria Arcana` (+1 dano por 5s), `Tempo Arcano` (50% mais lento por 3s), `Tempestade` (próximo tiro aniquila comuns / fere elites e chefes) e `Precisão` (retículo reduzido e destacado em esmeralda por 5s).
- **Sistema de Rank Final (`S / A / B / C / D`):** Avalia abates, precisão, tempo, fugas, maior combo e uso de feitiços especiais.

---

## 5. Interface, Identidade Visual e Assets

- **Tipografia Bundled (`Assets/Fonts/`):** `FantasyTitle.ttf` (serifada para títulos) e `FantasyUI.ttf` (limpa para HUD e textos em português, sem emojis sem suporte/glifos quebrados).
- **Tema Dark-Fantasy 9-Slice (`Assets/Sprites/UI/Theme/`):** `PanelFrame_Ornate`, `Button_Gold`, `Button_Stone`, `SpellSlot_Frame`, `ManaOrb_Glow`, `ManaCrystal_On/Off`, `Heart_Full/Empty`, `SpellMedallion_Frame` e `MenuVignette_Left`.
- **Mira Customizada:** `Assets/Sprites/UI/crosshair_reticle.png` (mira dourada com gemas vermelhas).
- **HUD Flutuante:** Placas compactas nos cantos e centro sem barras opacas ocupando o campo de visão; botões do menu com `UIButtonJuice` e sliders de áudio isolados em `SettingsPanel` e `PauseMenuPanel`.

---

## 6. Auditoria Técnica e Correções Recentes Aplicadas

Durante a verificação completa dos arquivos foram identificados e corrigidos os seguintes pontos:

1. **Reiniciar vs. Menu Principal:** `RestartGame()` passou a reiniciar diretamente a partida (`autoStartNextLoad`) em vez de devolver o jogador à tela inicial.
2. **Precisão > 100% e Cooldowns:** `Lightning` e `Area` agora contabilizam no máximo 1 acerto por disparo para cálculo de precisão (`0–100%`), `totalDamageDealt` contabiliza exatamente o crítico em área, e `HandleSpecialShot` checa `IsSpellReady` antes de consumir `Storm` ou `isFirstShotAttempt`.
3. **Race Condition em Fuga Letal:** `WizardSpawner.HandleEscaped` dispara `OnWizardEscaped` antes de avaliar `OnWaveCompleted`, impedindo transição falsa de fase quando a última fuga gera derrota.
4. **Ciclo de Vida de Corrotinas, Projéteis e Cores:**
   - `BossProjectileInstance` agora dirige seu voo em `Update()` para nunca congelar no ar caso o chefe seja abatido.
   - `FreezeRoutine`, `FlashDamageRoutine` e `Die` coordenam `pulseCoroutine`, `IsTeleporting` e `baseTint`, evitando inimigos congelados sem cor azul-gelo ou corpos presos em vermelho.
   - `customDamageSound` (ex.: teleporte do Fantasma) passou a ser reproduzido em acertos não letais.
   - `ProtectAllyRoutine` corrigido para não entrar em loop infinito sem aliados; recuo do `Dourado` limitado aos limites da arena.
5. **Tempo Real, Pausa e Áudio:**
   - Buffs de `PowerUpManager` pausam quando `Time.timeScale == 0`; varinha e recarga usam `unscaledDeltaTime` para não ficarem 2× mais lentas em `Tempo Arcano`.
   - `FadeToClip` lê `isMuted` e `bgmVolume` em tempo real durante o crossfade.
   - `ProjectSetupWizard.LoadFrames` ganhou fallback para `<prefix>.png` no índice `0`, corrigindo o carregamento completo dos 2 frames de ataque do `Goblin_Comum` (`attack.png` + `attack_1.png`).
6. **Zero Warnings Unity 6:** Substituídas todas as chamadas obsoletas `OverlapCircleNonAlloc`/`OverlapPointNonAlloc` por `Physics2D.OverlapCircle`/`OverlapPoint` com `ContactFilter2D.noFilter`.
7. **Higiene de Repositório (`.gitignore`):** Adicionadas regras para ignorar `*.log` e pastas locais auxiliares (`My project/`, `aula/`).

---

## 7. Próximo Passo Obrigatório no Unity Editor & Roadmap

### Ação no Unity Editor
Sempre que atualizar scripts do `ProjectSetupWizard`, abra o projeto no **Unity 6 Editor** e execute no menu superior:
> **`Tools > Setup Mata Goblin Complete`**

Isso regenera `Assets/Scenes/Gameplay.unity` e os `ScriptableObjects` com todas as referências atualizadas (título **Mata Goblin**, obstáculos 2.5D recortados, 5 músicas padrão de fase, balanceamento suave das Fases 1–5 e Grimório revisado).

### Próximos Passos Sugeridos (Roadmap)
- [x] Implementar camada de **cenário 2.5D com obstáculos/coberturas em primeiro plano** (pedras, pilares, caixas com `sortingOrder` intermediário onde goblins passam atrás e na frente + parallax reativo ao mouse).
- [x] Persistência de **Recorde (High Score)** e melhor **Rank** via `PlayerPrefs`.
- [ ] Adicionar sprites e integrar na rotação de ondas os 4 arquétipos já estruturados na arquitetura (`Escudo`, `Curandeiro`, `Ilusionista` e `Bombardeiro`).
- [ ] Gerar build final Windows (`Tools > Build Game Standalone`) após ativação da licença no Unity Hub.
