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
- **4 Tipos de Magos**:
  - 🧙 **Mago Comum**: 1 HP, 1 Ponto. Flutua suavemente pelo cenário.
  - 🏃 **Mago Rápido**: 2 HP, 2 Pontos. Ao tomar o 1º tiro, reage com *squash & stretch* e foge em disparada!
  - ⭐ **Mago Dourado**: 3 HP, 5 Pontos. Brilho pulsante e movimentação rápida em linha reta.
  - 👻 **Mago Fantasma**: 4 HP, 3 Pontos. Transparência mágica e **teleporte instantâneo** pelo mapa ao sofrer dano!
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