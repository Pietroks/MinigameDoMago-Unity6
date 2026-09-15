#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WizardGame.Core;
using WizardGame.Data;
using WizardGame.Entities;
using WizardGame.Input;
using WizardGame.UI;

namespace WizardGame.EditorTools
{
    public static class ProjectSetupWizard
    {
        [MenuItem("Tools/Setup Wizard Minigame Complete")]
        public static void SetupProject()
        {
            Debug.Log("Iniciando reconstrucao completa do Minigame do Mago...");

            ConfigureSprites();
            var wizardDatas = CreateWizardScriptableObjects();
            var wizardPrefab = CreateWizardPrefab();
            SetupGameplayScene(wizardDatas, wizardPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Minigame do Mago reconstruido com AudioListener, BGM, SFX e Health Bars!");
        }

        private static void ConfigureSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
            }
        }

        private static List<WizardDataSO> CreateWizardScriptableObjects()
        {
            string folder = "Assets/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

            var list = new List<WizardDataSO>();

            // Comum
            var comum = GetOrCreateSO<WizardDataSO>(folder + "/Wizard_Comum.asset");
            comum.wizardType = WizardType.Comum;
            comum.displayName = "Mago Comum";
            comum.maxHealth = 1;
            comum.pointsOnDefeat = 1;
            comum.escapeTimeSeconds = 5.0f;
            comum.spawnWeight = 65;
            comum.escapePenalty = 1;
            comum.baseTint = Color.white;
            comum.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Wizards/maguinho.webp");
            comum.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            EditorUtility.SetDirty(comum);
            list.Add(comum);

            // Rapido
            var rapido = GetOrCreateSO<WizardDataSO>(folder + "/Wizard_Rapido.asset");
            rapido.wizardType = WizardType.Rapido;
            rapido.displayName = "Mago Rapido";
            rapido.maxHealth = 2;
            rapido.pointsOnDefeat = 2;
            rapido.escapeTimeSeconds = 6.0f;
            rapido.spawnWeight = 20;
            rapido.escapePenalty = 2;
            rapido.baseTint = new Color(1f, 0.75f, 0.75f);
            rapido.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Wizards/maguinho2.png");
            rapido.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");
            rapido.customDamageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/Zé-Wilker-Filho-da-puta (mp3cut.net).mp3");
            EditorUtility.SetDirty(rapido);
            list.Add(rapido);

            // Dourado
            var dourado = GetOrCreateSO<WizardDataSO>(folder + "/Wizard_Dourado.asset");
            dourado.wizardType = WizardType.Dourado;
            dourado.displayName = "Mago Dourado";
            dourado.maxHealth = 3;
            dourado.pointsOnDefeat = 5;
            dourado.escapeTimeSeconds = 7.0f;
            dourado.spawnWeight = 10;
            dourado.escapePenalty = 1;
            dourado.baseTint = new Color(1f, 0.95f, 0.4f);
            dourado.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Wizards/mago-nivel-3.png");
            dourado.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            dourado.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(dourado);
            list.Add(dourado);

            // Fantasma
            var fantasma = GetOrCreateSO<WizardDataSO>(folder + "/Wizard_Fantasma.asset");
            fantasma.wizardType = WizardType.Fantasma;
            fantasma.displayName = "Mago Fantasma";
            fantasma.maxHealth = 4;
            fantasma.pointsOnDefeat = 3;
            fantasma.escapeTimeSeconds = 9.0f;
            fantasma.spawnWeight = 15;
            fantasma.escapePenalty = 1;
            fantasma.baseTint = new Color(0.7f, 0.85f, 1f, 0.85f);
            fantasma.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Wizards/mago-1-2.png");
            fantasma.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            fantasma.customDamageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/dbz-teleport.mp3");
            EditorUtility.SetDirty(fantasma);
            list.Add(fantasma);

            return list;
        }

        private static T GetOrCreateSO<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static WizardController CreateWizardPrefab()
        {
            string folder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Prefabs");
            string prefabPath = folder + "/Wizard_Base.prefab";

            GameObject go = new GameObject("Wizard_Base");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var controller = go.AddComponent<WizardController>();
            SetPrivateField(controller, "spriteRenderer", sr);
            SetPrivateField(controller, "hitCollider", col);
            SetPrivateField(controller, "targetHeight", 1.4f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            GameObject.DestroyImmediate(go);
            return prefab.GetComponent<WizardController>();
        }

        private static void SetupGameplayScene(List<WizardDataSO> wizardDatas, WizardController wizardPrefab)
        {
            string scenePath = "Assets/Scenes/Gameplay.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera com AudioListener OBRIGATÓRIO
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.0f;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // OUVINTE DE ÁUDIO (Sem ele o jogo fica 100% mudo!)
            camGo.AddComponent<AudioListener>();

            // 2. SoundManager
            GameObject soundGo = new GameObject("SoundManager");
            var soundMgr = soundGo.AddComponent<SoundManager>();
            soundMgr.bgmMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/harp-piano-dreamy-flashback-jam-fx-1-00-07.mp3");
            soundMgr.normalShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/tiro.mp3");
            soundMgr.strongShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/raio.mp3");
            soundMgr.teleportSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/dbz-teleport.mp3");
            soundMgr.victorySound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/award-winners.mp3");
            soundMgr.defeatSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/heavy-thunder-sound-effect-no-copyright-338980.mp3");
            soundMgr.hitDamageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/tiro.mp3");

            var commonDeathsList = new List<AudioClip>();
            for (int i = 1; i <= 16; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/SFX/CommonDeaths/morte_comum_{i}.mp3");
                if (clip != null) commonDeathsList.Add(clip);
            }
            SetPrivateField(soundMgr, "commonDeathSounds", commonDeathsList);

            // 3. Spawner
            GameObject spawnerGo = new GameObject("WizardSpawner");
            var spawner = spawnerGo.AddComponent<WizardSpawner>();
            SetPrivateField(spawner, "wizardPrefab", wizardPrefab);
            SetPrivateField(spawner, "wizardTypes", wizardDatas);

            // 4. Mira (Crosshair)
            GameObject crosshairGo = new GameObject("Crosshair_Aim");
            var crossSr = crosshairGo.AddComponent<SpriteRenderer>();
            crossSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cursor/wand_cursor_cartoon_96.png");
            crossSr.sortingOrder = 100;
            crosshairGo.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

            // 5. Varinha POV (Primeira Pessoa)
            GameObject wandGo = new GameObject("Wand_POV");
            var wandSr = wandGo.AddComponent<SpriteRenderer>();
            wandSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cursor/wand_cursor_cartoon_96.png");
            wandSr.sortingOrder = 90;
            wandGo.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            GameObject flashGo = new GameObject("MuzzleFlash");
            flashGo.transform.SetParent(wandGo.transform, false);
            flashGo.transform.localPosition = new Vector3(0.5f, 0.5f, 0f);
            var flashSr = flashGo.AddComponent<SpriteRenderer>();
            flashSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cursor/wand_cursor_cartoon_96.png");
            flashSr.color = Color.yellow;
            flashSr.sortingOrder = 91;
            flashSr.enabled = false;

            // 6. WeaponSystem
            GameObject weaponGo = new GameObject("WeaponSystem");
            var weaponSystem = weaponGo.AddComponent<WeaponSystem>();
            SetPrivateField(weaponSystem, "wandTransform", wandGo.transform);
            SetPrivateField(weaponSystem, "crosshairTransform", crosshairGo.transform);
            SetPrivateField(weaponSystem, "muzzleFlash", flashSr);

            // 7. PlayerInputHandler
            GameObject inputGo = new GameObject("PlayerInputHandler");
            var inputHandler = inputGo.AddComponent<PlayerInputHandler>();
            SetPrivateField(inputHandler, "weaponSystem", weaponSystem);
            SetPrivateField(inputHandler, "clickToleranceRadius", 0.65f);

            // 8. Canvas UI
            GameObject canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // --- HUD IN-GAME ---
            GameObject hudGo = CreatePanel(canvasGo.transform, "InGameHUD", new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0, 0, 0, 0));

            // Top Bar Background
            GameObject topBar = CreatePanel(hudGo.transform, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Color(0.08f, 0.08f, 0.14f, 0.88f));
            var topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.pivot = new Vector2(0.5f, 1f);
            topBarRect.sizeDelta = new Vector2(0f, 85f);
            topBarRect.anchoredPosition = Vector2.zero;

            var scoreText = CreateUIText(topBar.transform, "ScoreText", "Magos: 0 / 50", 26, Color.white, new Vector2(25f, -22f), new Vector2(0f, 1f), defaultFont);
            var escapeText = CreateUIText(topBar.transform, "EscapeText", "Escaparam: 0 / 15", 22, new Color(1f, 0.6f, 0.6f), new Vector2(25f, -54f), new Vector2(0f, 1f), defaultFont);

            var ammoText = CreateUIText(topBar.transform, "AmmoText", "MANA: 8 / 8  [R]", 24, Color.cyan, new Vector2(0f, -42f), new Vector2(0.5f, 1f), defaultFont, TextAnchor.MiddleCenter);

            var strongText = CreateUIText(topBar.transform, "StrongShotText", "TIRO FORTE [RMB]: PRONTO!", 24, Color.green, new Vector2(-220f, -42f), new Vector2(1f, 1f), defaultFont, TextAnchor.MiddleRight);

            var muteBtnObj = CreateButton(topBar.transform, "MuteButton", "SOM: ATIVO", new Vector2(-25f, -42f), new Vector2(1f, 1f), new Vector2(150f, 44f), defaultFont, new Color(0.2f, 0.2f, 0.35f));
            var muteBtn = muteBtnObj.GetComponent<Button>();
            var muteBtnText = muteBtnObj.GetComponentInChildren<Text>();

            // Bottom Bar Controls Hint
            GameObject bottomBar = CreatePanel(hudGo.transform, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Color(0.05f, 0.05f, 0.08f, 0.75f));
            var botBarRect = bottomBar.GetComponent<RectTransform>();
            botBarRect.pivot = new Vector2(0.5f, 0f);
            botBarRect.sizeDelta = new Vector2(0f, 40f);
            botBarRect.anchoredPosition = Vector2.zero;
            var hintText = CreateUIText(bottomBar.transform, "ControlsHint", "[LMB] Atirar  |  [RMB] Tiro Forte  |  [R] Recarregar Mana  |  [ESC / P] Pausar", 18, new Color(0.85f, 0.85f, 0.85f), new Vector2(0f, 20f), new Vector2(0.5f, 0f), defaultFont, TextAnchor.MiddleCenter);

            // --- MENU INICIAL ---
            GameObject startMenu = CreatePanel(canvasGo.transform, "StartMenuPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0.04f, 0.04f, 0.08f, 0.95f));

            CreateUIText(startMenu.transform, "Title", "MINIGAME DO MAGO", 58, Color.yellow, new Vector2(0f, 260f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);
            CreateUIText(startMenu.transform, "Subtitle", "Defenda a torre e nao deixe os magos escaparem!", 24, Color.white, new Vector2(0f, 195f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            var playBtnObj = CreateButton(startMenu.transform, "PlayButton", "JOGAR AGORA", new Vector2(0f, 60f), new Vector2(0.5f, 0.5f), new Vector2(320f, 65f), defaultFont, new Color(0.5f, 0.1f, 0.8f));
            var playBtn = playBtnObj.GetComponent<Button>();

            var instrBtnObj = CreateButton(startMenu.transform, "InstructionsButton", "COMO JOGAR / CONTROLES", new Vector2(0f, -20f), new Vector2(0.5f, 0.5f), new Vector2(320f, 55f), defaultFont, new Color(0.15f, 0.3f, 0.6f));
            var instrBtn = instrBtnObj.GetComponent<Button>();

            var quitBtnObj = CreateButton(startMenu.transform, "QuitButton", "SAIR DO JOGO", new Vector2(0f, -95f), new Vector2(0.5f, 0.5f), new Vector2(320f, 50f), defaultFont, new Color(0.35f, 0.1f, 0.1f));
            var quitBtn = quitBtnObj.GetComponent<Button>();

            // --- PAINEL DE INSTRUÇÕES ---
            GameObject instrPanel = CreatePanel(startMenu.transform, "InstructionsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.08f, 0.08f, 0.16f, 0.98f));
            var instrRect = instrPanel.GetComponent<RectTransform>();
            instrRect.sizeDelta = new Vector2(750f, 520f);
            instrRect.anchoredPosition = Vector2.zero;

            CreateUIText(instrPanel.transform, "InstrTitle", "COMO JOGAR & CONTROLES", 34, Color.yellow, new Vector2(0f, 215f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            string instrBody = "OBJETIVO:\n" +
                               "Abata 50 magos para vencer! Se 15 escaparem, voce perde!\n\n" +
                               "CONTROLES:\n" +
                               "  • Clique Esquerdo (LMB): Tiro de Mana Normal (Dano 1)\n" +
                               "  • Clique Direito (RMB): Tiro Forte Arcano (Dano 3 - Cooldown 3s)\n" +
                               "  • Tecla [R]: Recarregar Mana (Capacidade: 8 feitiços)\n" +
                               "  • Tecla [ESC] ou [P]: Pausar o Jogo\n\n" +
                               "TIPOS DE MAGOS & VIDA:\n" +
                               "  • Comum (1 HP): Morre com 1 tiro.\n" +
                               "  • Rapido (2 HP): Barra de vida visivel! Foge apos o 1º hit!\n" +
                               "  • Dourado (3 HP - 5 Pts): Barra de vida! Corre suave pelo mapa!\n" +
                               "  • Fantasma (4 HP): Barra de vida! Teleporta pelo cenario!";
            CreateUIText(instrPanel.transform, "InstrBody", instrBody, 20, Color.white, new Vector2(0f, 15f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleLeft);

            var closeInstrBtnObj = CreateButton(instrPanel.transform, "CloseInstrBtn", "ENTENDIDO! VOLTAR", new Vector2(0f, -200f), new Vector2(0.5f, 0.5f), new Vector2(260f, 50f), defaultFont, new Color(0.2f, 0.5f, 0.2f));
            var closeInstrBtn = closeInstrBtnObj.GetComponent<Button>();
            instrPanel.SetActive(false);

            // --- MENU DE PAUSA ---
            GameObject pausePanel = CreatePanel(canvasGo.transform, "PauseMenuPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.05f, 0.05f, 0.1f, 0.95f));
            var pauseRect = pausePanel.GetComponent<RectTransform>();
            pauseRect.sizeDelta = new Vector2(450f, 380f);
            pauseRect.anchoredPosition = Vector2.zero;

            CreateUIText(pausePanel.transform, "PauseTitle", "JOGO PAUSADO", 40, Color.white, new Vector2(0f, 130f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            var resumeBtnObj = CreateButton(pausePanel.transform, "ResumeBtn", "CONTINUAR", new Vector2(0f, 40f), new Vector2(0.5f, 0.5f), new Vector2(280f, 55f), defaultFont, new Color(0.2f, 0.6f, 0.2f));
            var resumeBtn = resumeBtnObj.GetComponent<Button>();

            var restartBtnObj = CreateButton(pausePanel.transform, "RestartBtn", "REINICIAR", new Vector2(0f, -30f), new Vector2(0.5f, 0.5f), new Vector2(280f, 50f), defaultFont, new Color(0.2f, 0.35f, 0.6f));
            var restartBtn = restartBtnObj.GetComponent<Button>();

            var pauseMenuBtnObj = CreateButton(pausePanel.transform, "PauseMenuBtn", "MENU PRINCIPAL", new Vector2(0f, -95f), new Vector2(0.5f, 0.5f), new Vector2(280f, 50f), defaultFont, new Color(0.4f, 0.15f, 0.15f));
            var pauseMenuBtn = pauseMenuBtnObj.GetComponent<Button>();
            pausePanel.SetActive(false);

            // --- MENU DE FIM DE JOGO ---
            GameObject endPanel = CreatePanel(canvasGo.transform, "GameOverPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.04f, 0.04f, 0.08f, 0.96f));
            var endRect = endPanel.GetComponent<RectTransform>();
            endRect.sizeDelta = new Vector2(600f, 420f);
            endRect.anchoredPosition = Vector2.zero;

            var endTitleText = CreateUIText(endPanel.transform, "EndTitle", "VITORIA!", 46, Color.yellow, new Vector2(0f, 140f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);
            var endScoreText = CreateUIText(endPanel.transform, "EndScore", "Magos Abatidos: 50", 26, Color.white, new Vector2(0f, 45f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            var endRestartBtnObj = CreateButton(endPanel.transform, "EndRestartBtn", "JOGAR NOVAMENTE", new Vector2(0f, -60f), new Vector2(0.5f, 0.5f), new Vector2(300f, 55f), defaultFont, new Color(0.5f, 0.1f, 0.8f));
            var endRestartBtn = endRestartBtnObj.GetComponent<Button>();

            var endMenuBtnObj = CreateButton(endPanel.transform, "EndMenuBtn", "MENU PRINCIPAL", new Vector2(0f, -130f), new Vector2(0.5f, 0.5f), new Vector2(300f, 50f), defaultFont, new Color(0.25f, 0.25f, 0.35f));
            var endMenuBtn = endMenuBtnObj.GetComponent<Button>();
            endPanel.SetActive(false);

            // 9. UIManager Wiring
            var uiMgr = canvasGo.AddComponent<UIManager>();
            SetPrivateField(uiMgr, "startMenuPanel", startMenu);
            SetPrivateField(uiMgr, "pauseMenuPanel", pausePanel);
            SetPrivateField(uiMgr, "instructionsPanel", instrPanel);
            SetPrivateField(uiMgr, "gameOverPanel", endPanel);
            SetPrivateField(uiMgr, "inGameHUD", hudGo);

            SetPrivateField(uiMgr, "scoreText", scoreText);
            SetPrivateField(uiMgr, "escapeText", escapeText);
            SetPrivateField(uiMgr, "ammoText", ammoText);
            SetPrivateField(uiMgr, "strongShotText", strongText);
            SetPrivateField(uiMgr, "controlsHintText", hintText);
            SetPrivateField(uiMgr, "muteButton", muteBtn);
            SetPrivateField(uiMgr, "muteButtonText", muteBtnText);

            SetPrivateField(uiMgr, "endTitleText", endTitleText);
            SetPrivateField(uiMgr, "endScoreText", endScoreText);
            SetPrivateField(uiMgr, "restartButton", endRestartBtn);
            SetPrivateField(uiMgr, "endMainMenuButton", endMenuBtn);

            SetPrivateField(uiMgr, "playButton", playBtn);
            SetPrivateField(uiMgr, "instructionsButton", instrBtn);
            SetPrivateField(uiMgr, "closeInstructionsButton", closeInstrBtn);
            SetPrivateField(uiMgr, "quitButton", quitBtn);

            SetPrivateField(uiMgr, "resumeButton", resumeBtn);
            SetPrivateField(uiMgr, "pauseRestartButton", restartBtn);
            SetPrivateField(uiMgr, "pauseMainMenuButton", pauseMenuBtn);

            // 10. GameManager Wiring
            GameObject gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            SetPrivateField(gm, "spawner", spawner);
            SetPrivateField(gm, "inputHandler", inputHandler);
            SetPrivateField(gm, "weaponSystem", weaponSystem);
            SetPrivateField(gm, "uiManager", uiMgr);

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static Text CreateUIText(Transform parent, string name, string text, int fontSize, Color color, Vector2 anchoredPos, Vector2 anchor, Font font, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(650f, 60f);

            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return t;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 anchor, Vector2 size, Font font, Color bgColor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.3f);
            btn.colors = colors;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var t = textGo.AddComponent<Text>();
            t.text = label;
            t.font = font;
            t.fontSize = 20;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            return go;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
#endif