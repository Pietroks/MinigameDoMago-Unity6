# 🧙‍♂️ Minigame do Mago (Unity 6 / 6000.x)

Recriação e refatoração completa do clássico jogo Point-and-Click / Shooting Gallery 2D para a engine **Unity 6 (6000.x)**.

---

## 🎮 Mecânicas e Funcionalidades

- **Visão em Primeira Pessoa (Arma POV)**:
  - Varinha mágica estilizada no canto da tela que acompanha suavemente a mira do jogador.
  - Animação de coice/recuo (*kickback*) e feixe luminoso ao disparar feitiços.
- **Mira Gráfica (Crosshair)**:
  - Cursor de mira dinâmico com tolerância arcade de acerto imediato.
- **Sistema de Munição (Mana)**:
  - Capacidade de **8 feitiços**.
  - Recarga manual com a tecla `[R]` ou automática ao esvaziar o pente (duração de 1.2s).
- **Tiro Forte Arcano**:
  - Acionado com **Botão Direito do Mouse (RMB)**. Causa **3 de dano**.
  - Cooldown de 3.0 segundos com indicador de recarga em tempo real no HUD.
- **4 Tipos de Goblins (Spritesheets & Animações Dedicadas)**:
  - 👺 **Goblin Comum**: 1 HP, 1 Ponto. Patrulha o cenário e ataca com facas de perto. Animações: Idle, Walk, Attack, Death.
  - 🏃 **Goblin Fugitivo**: 2 HP, 2 Pontos. Ao tomar dano, salta em pânico e foge em disparada acelerada. Animações: Idle, Walk, Run, Jump, Death.
  - 🛡️ **Goblin Dourado**: 3 HP, 5 Pontos. Rápido e agressivo com investidas (*Dash*) e postura defensiva com escudo (*Shield*). Vale muitos pontos! Animações: Idle, Walk, Dash, Shield, Death.
  - 🌀 **Goblin Fantasma**: 4 HP, 3 Pontos. Levitação mágica e teleporte instantâneo através de portais dimensionais ao sofrer impacto. Animações: Idle, Float, Portal, Attack, Death.
- **Sistema de Combos & Headshot**:
  - Multiplicador de pontuação progressivo (x1 a x4) por abates em sequência.
  - Bônus de precisão (*Headshot / Acerto Perfeito*): +1 ponto extra ao atingir o ponto fraco superior do goblin.
  - Errar tiros ou deixar goblins escaparem quebra a sequência de combo!
- **Condições de Partida**:
  - **Vitória**: Abater **50 magos**.
  - **Derrota**: Deixar **15 magos escaparem**.
- **Interface Completa (HUD e Menus)**:
  - Menu Inicial (Jogar, Instruções/Controles, Sair).
  - Menu de Pausa (`[ESC]` ou `[P]`).
  - Botão de Som / Mute com alternância instantânea.
  - Painel de Vitória e Derrota com reinício rápido.

---

## ⌨️ Controles

| Comando | Ação |
| :--- | :--- |
| **Clique Esquerdo (LMB)** | Tiro Normal de Mana (Dano 1) |
| **Clique Direito (RMB)** | Tiro Forte Arcano (Dano 3 - Recarga 3s) |
| **Tecla [R]** | Recarregar Mana da Varinha |
| **Tecla [ESC] ou [P]** | Pausar / Retomar Jogo |

---

## 🛠️ Tecnologias Utilizadas

- **Engine**: Unity 6 (`6000.6.0f1` ou superior)
- **Linguagem**: C# (.NET Standard 2.1)
- **Render Pipeline**: 2D Universal Render Pipeline (URP 2D) com Direct3D 12
- **Gerenciamento de Memória**: `UnityEngine.Pool.ObjectPool<T>` (Zero alocação de Garbage Collection)
- **Arquitetura de Dados**: ScriptableObjects desacoplados (`WizardDataSO`)

---

## 🚀 Como Abrir no Unity Editor

1. Abra o **Unity Hub**.
2. Clique em **Add > Add project from disk**.
3. Selecione a pasta deste repositório.
4. Abra a cena `Assets/Scenes/Gameplay.unity`.
5. Clique no botão **Play**!