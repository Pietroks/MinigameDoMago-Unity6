# 🧙‍♂️ Minigame do Mago (Unity 6 / 6000.x)

Recriação e refatoração completa do clássico jogo Point-and-Click / Shooting Gallery 2D para a engine **Unity 6 (6000.x)** com **Universal Render Pipeline (URP 2D)**.

---

## 🎮 Mecânicas e Funcionalidades

### 1. Visão em Primeira Pessoa (Arma POV) & Mira de Precisão
- **Varinha Mágica com Luva de Couro**: Posicionada em primeira pessoa, com inércia de mouse (*sway*), oscilação orgânica (*bobbing*), recuo físico (*recoil*) e brilho elemental pulsante no cristal que muda de cor conforme o feitiço selecionado.
- **Mira Dinâmica (Crosshair)**: Retículo de alta precisão que detecta passagem sobre alvos (*hover* com mudança de cor) e exibe feedback de impacto (*hitmarker* branco para acerto normal e vermelho para headshot).

### 2. Arsenal Mágico de Feitiços
O jogador possui um arsenal de **5 feitiços**, alternáveis por teclas de atalho ou pela roda do mouse (**Scroll Wheel**):
- 🔥 **Tiro Normal** (`LMB`): Disparo básico de mana. Causa **1 de dano**. Capacidade de **8 feitiços**, com recarga automática ou manual (`[R]`, 1.2s).
- 💥 **Tiro Arcano** (`[1]` ou `[Q]` ou `RMB`): Projétil concentrado de alta potência. Causa **3 de dano**. Recarga: **3.0s**.
- ❄️ **Feitiço de Gelo** (`[2]` ou `[E]`): Projétil criogênico que **congela o goblin por 2.5s**, paralisando movimentação e ataques com tonalidade azulada. Causa **1 de dano**. Recarga: **4.0s**.
- ⚡ **Raio em Cadeia** (`[3]` ou `[F]`): Descarga elétrica que atinge o alvo primário com **2 de dano** e salta eletrocutando até **2 goblins próximos** com **1 de dano extra**. Recarga: **4.5s**.
- 🌀 **Explosão em Área** (`[4]`, `[C]` ou `[Espaço]`): Onda de choque expansiva de 2.5 metros que causa **2 de dano** em **todos** os inimigos no raio da explosão. Recarga: **5.5s**.

### 3. Sistema de Ondas Progressivas (Wave System)
A partida é dividida em **5 Ondas Táticas** com transições animadas, banners informativos, bônus de pontuação e restauração de mana:
- **Onda 1 - Primeiros Invasores**: 6 Goblins Comuns (aquecimento e ritmo).
- **Onda 2 - Patrulha Acelerada**: 4 Comuns + 3 Fugitivos (aumento de velocidade e dispersão).
- **Onda 3 - Guarda Dourada & O 1º Chefe**: 3 Fugitivos + 2 Dourados + **1 Grande Goblin (Boss)**.
- **Onda 4 - Cerco Espectral**: 3 Dourados + 3 Fantasmas + **1 Xamã do Caos (Boss)**.
- **Onda 5 - Batalha Decisiva**: Onda contínua culminando no **Lorde Carmesim (40º)** e no **Goblin Rei Supremo (50º)**!

### 4. Os 5 Grandes Chefes (Boss a cada 10 Goblins)
A cada marca de 10 goblins abatidos surge um Chefe com barra de vida colorida, tamanho imponente e habilidades devastadoras:
1. 👑 **10º Goblin - Grande Goblin** (15 HP, +25 Pts): Golpe sísmico com tremor de tela e investida brutal.
2. 🔥 **20º Goblin - Xamã do Caos** (25 HP, +50 Pts): Projéteis ígneos de longa distância e erupções vulcânicas em área.
3. 🔮 **30º Goblin - Feiticeiro Espectral** (35 HP, +100 Pts): Portais de teleporte contínuo, invisibilidade temporária e invocação de servos.
4. 🩸 **40º Goblin - Lorde Carmesim** (50 HP, +150 Pts): Cetro sísmico devastador, orbes sanguíneas e aura carmesim de impacto.
5. ⚔️ **50º Goblin - Goblin Rei Supremo** (80 HP, +500 Pts): O Boss Final definitivo! Terremoto colossal, triplo magma explosivo e invocação real. **Derrotar o Goblin Supremo concede Vitória Imediata!**

### 5. Inimigos Regulares
- 👺 **Goblin Comum**: 1 HP | 1 Ponto. Patrulha e ataca com adagas.
- 🏃 **Goblin Fugitivo**: 2 HP | 2 Pontos. Ao sofrer dano, salta em pânico e foge em disparada acelerada (180% de velocidade).
- 🛡️ **Goblin Dourado**: 3 HP | 5 Pontos. Rápido e agressivo com investidas (*Dash*) e postura defensiva com escudo impenetrável temporário.
- 🌀 **Goblin Fantasma**: 4 HP | 3 Pontos. Levitação mágica e teleporte instantâneo através de portais dimensionais ao sofrer impacto.

### 6. Sistema de Combos, Headshot & Bônus de Habilidade
- **Multiplicador Dinâmico**:
  - 1 a 4 abates: **x1**
  - 5 a 9 abates: **x2**
  - 10 a 19 abates: **x3**
  - 20+ abates: **x4**
- **Quebra de Combo**: Errar um disparo ou deixar um goblin escapar reseta imediatamente o multiplicador.

#### 🎯 Bônus de Habilidade (Skill Shots / Trick Plays)
Recompensas dinâmicas com popups coloridos animados e sons temáticos para jogadas estratégicas e de reflexo:
| Bônus | Pontos | Condição de Acionamento | Notificação HUD |
| :--- | :---: | :--- | :--- |
| **Primeiro Tiro** | **+5 pts** | Primeiro disparo da partida que acerta um goblin sem errar | `✨ PRIMEIRO TIRO! +5` (Dourado) |
| **Headshot** | **+2 pts** | Acerto no topo da cabeça do goblin (ponto fraco superior) | `🎯 HEADSHOT! +2` (Verde Esmeralda) |
| **Matar 3 Rapidamente** | **+5 pts** | 3 abates confirmados em uma janela de até 2.0 segundos | `🔥 TRIPLO ABATE RÁPIDO! +5` (Fogo Laranja) |
| **Interrupção de Teleporte** | **+10 pts** | Abater um Fantasma ou Chefe 3 durante o deslocamento dimensional | `🌀 INTERRUPÇÃO DIMENSIONAL! +10` (Púrpura) |
| **Desafio Dourado** | **+3 pts** | Superar o escudo do Goblin Dourado (3 HP) com o tiro normal de mana | `🛡️ DESAFIO DOURADO! +3` (Ouro) |
| **Feitiço Duplo** | **+10 pts** | Eliminar 2 ou mais goblins na mesma conjuração de feitiço (Raio ou Área) | `⚡ FEITIÇO DUPLO! +10` (Ciano Elétrico) |

### 7. Sistema de Telemetria e Rank da Partida (S / A / B / C / D)
Ao final da partida, o jogo calcula uma nota de desempenho baseada em **6 fatores ponderados**:
1. **Quantidade de Mortes**: 10 pts por mago abatido (até 500 pts).
2. **Precisão (%)**: Proporcional à taxa de acertos (até 250 pts).
3. **Tempo de Partida**: Bônus de velocidade para partidas rápidas (até 200 pts).
4. **Magos Escapados**: Bônus *Flawless* de +150 pts para zero fugas e penalidade de -30 pts por fuga.
5. **Melhor Combo**: 12 pts por nível de combo mantido (até 240 pts).
6. **Feitiços Fortes**: 15 pts por uso tático de magias especiais (até 150 pts).

#### Tabela de Ranks
| Rank | Pontuação | Título de Maestria | Emblema | Cor do Tema |
| :---: | :---: | :---: | :---: | :---: |
| **S** | **>= 1100 pts** | Arquimago Supremo | 👑 | Dourado Radiante (`#FFD700`) |
| **A** | **850 – 1099 pts** | Mago Mestre | 🔮 | Púrpura Arcano (`#C850FF`) |
| **B** | **650 – 849 pts** | Feiticeiro Adepto | ⚡ | Ciano Brilhante (`#00D0FF`) |
| **C** | **450 – 649 pts** | Mago Aprendiz | 📜 | Verde Esmeralda (`#66FF66`) |
| **D** | **< 450 pts** | Iniciante Novato | 🔰 | Cinza Prateado (`#B0B0B0`) |

---

## ⌨️ Tabela Completa de Controles

| Comando | Ação |
| :--- | :--- |
| **Botão Esquerdo (LMB)** | Disparar Feitiço Normal (Dano 1 - consome 1 mana) |
| **Botão Direito (RMB)** | Disparar Feitiço Especial Ativo |
| **Tecla [1] ou [Q]** | Selecionar Feitiço Arcano (Dano 3) |
| **Tecla [2] ou [E]** | Selecionar Feitiço de Gelo (Congela 2.5s) |
| **Tecla [3] ou [F]** | Selecionar Raio em Cadeia (Dano 2 + saltos elétricos) |
| **Tecla [4], [C] ou [Espaço]** | Selecionar Explosão em Área (Dano 2 em 2.5m) |
| **Roda do Mouse (Scroll)** | Alternar ciclicamente entre os Feitiços Especiais |
| **Tecla [R]** | Recarregar Mana da Varinha |
| **Tecla [ESC] ou [P]** | Pausar / Retomar a partida |

---

## 🛠️ Arquitetura Técnica & Otimizações

- **Engine**: Unity 6 (`6000.6.0f1`) com Universal Render Pipeline (URP 2D) e Direct3D 12.
- **Zero Garbage Collection (Zero-GC)**:
  - `UnityEngine.Pool.ObjectPool<WizardController>` com reaproveitamento integral de instâncias de goblins.
  - Consultas físicas não-alocativas (`Physics2D.OverlapPointNonAlloc` e `OverlapCircleNonAlloc`) com buffer pré-alocado.
  - Caching de `WaitForSeconds` no `FrameAnimator` e sprite estático para barras de vida.
- **Animações Frame-by-Frame Limpas**:
  - Algoritmo automatizado de corte com máscara de abas e BFS flood-fill eliminando 100% de linhas, bordas e textos descritivos das spritesheets.
- **Áudio Multicanal Dinâmico**:
  - Crossfade assíncrono independente de `Time.timeScale` e persistência de volumes via `PlayerPrefs`.

---

## 🚀 Como Executar o Projeto

1. Abra o **Unity Hub**.
2. Clique em **Add > Add project from disk** e selecione a pasta `UnityProject_MinigameDoMago`.
3. Abra a cena principal em `Assets/Scenes/Gameplay.unity`.
4. Pressione **Play** no editor ou execute o arquivo compilado em `Builds/MinigameDoMago.exe`!