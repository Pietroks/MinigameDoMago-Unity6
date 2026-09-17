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
        [MenuItem("Tools/Setup Goblin Minigame Complete")]
        public static void SetupProject()
        {
            Debug.Log("Iniciando reconstrucao completa com Goblins e Spritesheets...");

            ConfigureSprites();
            ConfigureAudio();
            var wizardDatas = CreateGoblinScriptableObjects();
            var wizardPrefab = CreateGoblinPrefab();
            SetupGameplayScene(wizardDatas, wizardPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Minigame dos Goblins configurado com sucesso!");
        }

        [MenuItem("Tools/Build Game Standalone")]
        public static void BuildGame()
        {
            Debug.Log("Iniciando reconstrucao e build de MinigameDoMago...");
            SetupProject();

            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene("Assets/Scenes/Gameplay.unity", true)
            };

            string buildFolder = "Builds";
            if (!System.IO.Directory.Exists(buildFolder))
            {
                System.IO.Directory.CreateDirectory(buildFolder);
            }

            string buildPath = buildFolder + "/MinigameDoMago.exe";

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = new[] { "Assets/Scenes/Gameplay.unity" };
            buildPlayerOptions.locationPathName = buildPath;
            buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
            buildPlayerOptions.options = BuildOptions.None;

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"BUILD CONCLUIDA COM SUCESSO! Total: {summary.totalSize} bytes em {buildPath}");
            }
            else
            {
                Debug.LogError($"BUILD FALHOU! Status: {summary.result} - Erros: {summary.totalErrors}");
                EditorApplication.Exit(1);
            }
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
                    bool dirty = false;
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        dirty = true;
                    }
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        dirty = true;
                    }
                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        dirty = true;
                    }
                    bool isSmooth = path.Contains("Background") || path.Contains("cenario") || path.Contains("Weapon") || path.Contains("crosshair") || path.Contains("UI");
                    FilterMode targetFilter = isSmooth ? FilterMode.Bilinear : FilterMode.Point;
                    if (importer.filterMode != targetFilter)
                    {
                        importer.filterMode = targetFilter;
                        dirty = true;
                    }
                    if (isSmooth && importer.maxTextureSize < 2048)
                    {
                        importer.maxTextureSize = 2048;
                        dirty = true;
                    }
                    if (dirty)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static void ConfigureAudio()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Music" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null)
                {
                    var settings = importer.defaultSampleSettings;
                    bool dirty = false;
                    if (settings.loadType != AudioClipLoadType.Streaming)
                    {
                        settings.loadType = AudioClipLoadType.Streaming;
                        dirty = true;
                    }
                    if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
                    {
                        settings.compressionFormat = AudioCompressionFormat.Vorbis;
                        dirty = true;
                    }
                    if (dirty)
                    {
                        importer.defaultSampleSettings = settings;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static List<WizardDataSO> CreateGoblinScriptableObjects()
        {
            string folder = "Assets/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

            var list = new List<WizardDataSO>();

            // 1. Goblin Comum: 1 HP, 1 Ponto, Velocidade 2.0 (Fiel à spritesheet oficial)
            var comum = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Comum.asset");
            comum.wizardType = WizardType.Comum;
            comum.displayName = "Goblin Comum";
            comum.maxHealth = 1;
            comum.pointsOnDefeat = 1;
            comum.moveSpeed = 2.0f;
            comum.escapeTimeSeconds = 5.5f;
            comum.spawnWeight = 50;
            comum.escapePenalty = 1;
            comum.baseTint = Color.white;
            comum.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/portrait.png");
            comum.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/idle_0.png");

            comum.idleFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "idle", 8);
            comum.walkFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "walk", 8);
            comum.hitFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "hit", 4);
            comum.deathFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "death", 6);

            comum.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            EditorUtility.SetDirty(comum);
            list.Add(comum);

            // 2. Goblin Fugitivo: HP 2, Pontos 2, Velocidade 5 (Fiel à spritesheet oficial)
            var fugitivo = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Fugitivo.asset");
            fugitivo.wizardType = WizardType.Fugitivo;
            fugitivo.displayName = "Goblin Fugitivo";
            fugitivo.maxHealth = 2;
            fugitivo.pointsOnDefeat = 2;
            fugitivo.moveSpeed = 5.0f;
            fugitivo.escapeTimeSeconds = 5.0f;
            fugitivo.spawnWeight = 25;
            fugitivo.escapePenalty = 2;
            fugitivo.baseTint = Color.white;
            fugitivo.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/portrait.png");
            fugitivo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/idle_0.png");
            fugitivo.idleFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "idle", 8);
            fugitivo.walkFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "walk", 8);
            fugitivo.hitFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "hit", 4);
            fugitivo.runFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "run", 8);
            fugitivo.deathFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "death", 6);
            fugitivo.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");
            fugitivo.customDamageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/Zé-Wilker-Filho-da-puta (mp3cut.net).mp3");
            EditorUtility.SetDirty(fugitivo);
            list.Add(fugitivo);

            // 3. Goblin Dourado: HP 3, Pontos 5, Velocidade 4 (Fiel à spritesheet oficial)
            var dourado = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Dourado.asset");
            dourado.wizardType = WizardType.Dourado;
            dourado.displayName = "Goblin Dourado";
            dourado.maxHealth = 3;
            dourado.pointsOnDefeat = 5;
            dourado.moveSpeed = 4.0f;
            dourado.escapeTimeSeconds = 6.0f;
            dourado.spawnWeight = 10;
            dourado.escapePenalty = 1;
            dourado.baseTint = Color.white;
            dourado.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/portrait.png");
            dourado.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/idle_0.png");
            dourado.idleFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "idle", 8);
            dourado.walkFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "walk", 8);
            dourado.hitFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "hit", 4);
            dourado.runFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "run", 8);
            dourado.deathFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "death", 6);
            dourado.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            dourado.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(dourado);
            list.Add(dourado);

            // 4. Goblin Fantasma: HP 4, Pontos 3, Velocidade 3 (Fiel à spritesheet oficial)
            var fantasma = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Fantasma.asset");
            fantasma.wizardType = WizardType.Fantasma;
            fantasma.displayName = "Goblin Fantasma";
            fantasma.maxHealth = 4;
            fantasma.pointsOnDefeat = 3;
            fantasma.moveSpeed = 3.0f;
            fantasma.escapeTimeSeconds = 7.5f;
            fantasma.spawnWeight = 15;
            fantasma.escapePenalty = 1;
            fantasma.baseTint = Color.white;
            fantasma.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/portrait.png");
            fantasma.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/idle_0.png");
            fantasma.idleFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "idle", 8);
            fantasma.walkFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "walk", 8);
            fantasma.hitFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "hit", 4);
            fantasma.teleportFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "teleport", 8);
            fantasma.deathFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "death", 6);
            fantasma.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            fantasma.customDamageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/dbz-teleport.mp3");
            EditorUtility.SetDirty(fantasma);
            list.Add(fantasma);

            return list;
        }

        private static Sprite[] LoadFrames(string folderPath, string prefix, int count)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{folderPath}/{prefix}_{i}.png");
                if (s != null) list.Add(s);
            }
            return list.ToArray();
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

        private static WizardController CreateGoblinPrefab()
        {
            string folder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Prefabs");
            string prefabPath = folder + "/Goblin_Base.prefab";

            GameObject go = new GameObject("Goblin_Base");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var anim = go.AddComponent<FrameAnimator>();
            var controller = go.AddComponent<WizardController>();
            SetPrivateField(controller, "spriteRenderer", sr);
            SetPrivateField(controller, "hitCollider", col);
            SetPrivateField(controller, "frameAnimator", anim);
            SetPrivateField(controller, "targetHeight", 1.85f);

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
            camGo.AddComponent<AudioListener>();

            // 1.1. Cenário de Fundo (Fase 1: Fortaleza Noturna - cenario1.png)
            GameObject bgGo = new GameObject("Background_Stage1");
            var bgSr = bgGo.AddComponent<SpriteRenderer>();
            bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Backgrounds/cenario1.png");
            bgSr.sortingOrder = -50;
            var bgScaler = bgGo.AddComponent<BackgroundScaler>();
            bgScaler.AdjustScale();

            // 2. SoundManager
            GameObject soundGo = new GameObject("SoundManager");
            var soundMgr = soundGo.AddComponent<SoundManager>();
            soundMgr.menuMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Goblin Dungeon Menu.mp3");
            soundMgr.gameplayPlaylist = new List<AudioClip>
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Mago na Mira.mp3"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Arcane Confrontation.mp3"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Sky Funeral.mp3"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Spooky Castle Comedy v2.mp3"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Wizard_s Mishap.mp3"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Tavern Menu of Doom.mp3")
            };
            soundMgr.bgmMusicClip = soundMgr.menuMusicClip;
            soundMgr.normalShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/tiro.mp3");
            soundMgr.strongShotSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/raio.mp3");
            soundMgr.lightningCastSound = soundMgr.strongShotSound;
            soundMgr.iceCastSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/dbz-teleport.mp3");
            soundMgr.areaCastSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/heavy-thunder-sound-effect-no-copyright-338980.mp3");
            soundMgr.teleportSound = soundMgr.iceCastSound;
            soundMgr.victorySound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/award-winners.mp3");
            soundMgr.defeatSound = soundMgr.areaCastSound;
            soundMgr.hitDamageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/tiro.mp3");
            soundMgr.headshotSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/surprise-sound-effect-99300.mp3");
            soundMgr.comboBreakSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");

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

            // 4. WeaponSystem
            GameObject weaponGo = new GameObject("WeaponSystem");
            var weaponSystem = weaponGo.AddComponent<WeaponSystem>();

            // 5. PlayerInputHandler
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

            var scoreText = CreateUIText(topBar.transform, "ScoreText", "Pontos: 0", 25, Color.white, new Vector2(25f, -22f), new Vector2(0f, 1f), defaultFont);
            var escapeText = CreateUIText(topBar.transform, "EscapeText", "Escaparam: 0 / 15", 21, new Color(1f, 0.6f, 0.6f), new Vector2(25f, -54f), new Vector2(0f, 1f), defaultFont);

            var waveText = CreateUIText(topBar.transform, "WaveText", "⚔️ ONDA 1 / 5  •  Restam: 5 / 5", 20, new Color(1f, 0.88f, 0.35f), new Vector2(0f, -20f), new Vector2(0.5f, 1f), defaultFont, TextAnchor.MiddleCenter);
            var ammoText = CreateUIText(topBar.transform, "AmmoText", "MANA: 8 / 8  [R]", 22, Color.cyan, new Vector2(0f, -52f), new Vector2(0.5f, 1f), defaultFont, TextAnchor.MiddleCenter);

            var strongText = CreateUIText(topBar.transform, "StrongShotText", "[RMB]: 💥 TIRO ARCANO (Dano 3)", 22, new Color(1f, 0.85f, 0.2f), new Vector2(-220f, -42f), new Vector2(1f, 1f), defaultFont, TextAnchor.MiddleRight);

            var muteBtnObj = CreateButton(topBar.transform, "MuteButton", "SOM: ATIVO", new Vector2(-25f, -42f), new Vector2(1f, 1f), new Vector2(150f, 44f), defaultFont, new Color(0.2f, 0.2f, 0.35f));
            var muteBtn = muteBtnObj.GetComponent<Button>();
            var muteBtnText = muteBtnObj.GetComponentInChildren<Text>();

            // --- SISTEMA DE COMBO CONTAINER NO HUD ---
            GameObject comboContainer = CreatePanel(hudGo.transform, "ComboContainer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Color(0.12f, 0.05f, 0.2f, 0.85f));
            var comboRect = comboContainer.GetComponent<RectTransform>();
            comboRect.pivot = new Vector2(0.5f, 1f);
            comboRect.sizeDelta = new Vector2(480f, 48f);
            comboRect.anchoredPosition = new Vector2(0f, -95f);

            var comboText = CreateUIText(comboContainer.transform, "ComboText", "COMBO x2 — 5 ABATES", 28, Color.yellow, new Vector2(0f, -24f), new Vector2(0.5f, 1f), defaultFont, TextAnchor.MiddleCenter);

            // Aviso de quebra de combo
            var comboBreakText = CreateUIText(hudGo.transform, "ComboBreakText", "COMBO QUEBRADO!", 24, Color.red, new Vector2(0f, -155f), new Vector2(0.5f, 1f), defaultFont, TextAnchor.MiddleCenter);

            // --- POPUP DE HEADSHOT ---
            GameObject headshotContainer = CreatePanel(hudGo.transform, "HeadshotPopup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0, 0, 0, 0));
            var hsRect = headshotContainer.GetComponent<RectTransform>();
            hsRect.sizeDelta = new Vector2(500f, 60f);
            hsRect.anchoredPosition = new Vector2(0f, 140f);
            var headshotText = CreateUIText(headshotContainer.transform, "HeadshotText", "🎯 HEADSHOT! +1 PONTO", 32, new Color(0.2f, 1f, 0.3f), Vector2.zero, new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            // --- BANNER DE ANÚNCIO E CONCLUSÃO DE ONDA ---
            GameObject waveBannerContainer = CreatePanel(hudGo.transform, "WaveBanner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.04f, 0.05f, 0.12f, 0.94f));
            var wbRect = waveBannerContainer.GetComponent<RectTransform>();
            wbRect.sizeDelta = new Vector2(760f, 130f);
            wbRect.anchoredPosition = new Vector2(0f, 160f);

            var wbTitle = CreateUIText(waveBannerContainer.transform, "BannerTitle", "⚔️ ONDA 1", 38, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 26f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);
            var wbSubtitle = CreateUIText(waveBannerContainer.transform, "BannerSubtitle", "5 Goblins Comuns se aproximam!", 22, Color.white, new Vector2(0f, -24f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);
            waveBannerContainer.SetActive(false);

            // --- ARMA POV EM PRIMEIRA PESSOA (VARINHA & MÃO) ---
            GameObject wandUIGo = new GameObject("Weapon_POV");
            wandUIGo.transform.SetParent(hudGo.transform, false);
            var wandRect = wandUIGo.AddComponent<RectTransform>();
            wandRect.anchorMin = new Vector2(1f, 0f);
            wandRect.anchorMax = new Vector2(1f, 0f);
            wandRect.pivot = new Vector2(0.83f, 0.06f);
            wandRect.sizeDelta = new Vector2(760f, 760f);
            wandRect.anchoredPosition = new Vector2(-40f, -35f);
            var wandImg = wandUIGo.AddComponent<Image>();
            wandImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Weapon/weapon.png");
            wandImg.raycastTarget = false;

            // Brilho pulsante e dinâmico na ponta do cristal da varinha
            GameObject glowGo = new GameObject("CrystalGlow");
            glowGo.transform.SetParent(wandUIGo.transform, false);
            var glowRect = glowGo.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.462f, 0.715f);
            glowRect.anchorMax = new Vector2(0.462f, 0.715f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(120f, 120f);
            glowRect.anchoredPosition = Vector2.zero;
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Weapon/crystal_glow.png");
            glowImg.color = new Color(0.2f, 0.92f, 1f, 0.9f);
            glowImg.raycastTarget = false;

            // --- BARRA DO ARSENAL MÁGICO (CAIXA DE FERRAMENTAS DO MAGO) ---
            GameObject spellBarGo = CreatePanel(hudGo.transform, "SpellArsenalBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Color(0.06f, 0.06f, 0.12f, 0.88f));
            var spellBarRect = spellBarGo.GetComponent<RectTransform>();
            spellBarRect.pivot = new Vector2(0.5f, 0f);
            spellBarRect.sizeDelta = new Vector2(820f, 60f);
            spellBarRect.anchoredPosition = new Vector2(0f, 44f);

            var (arcaneHighlight, arcaneCd) = CreateSpellSlot(spellBarGo.transform, "Slot_Arcane", "[1/Q] 💥 ARCANO", new Vector2(-300f, 30f), defaultFont);
            var (iceHighlight, iceCd) = CreateSpellSlot(spellBarGo.transform, "Slot_Ice", "[2/E] ❄️ GELO", new Vector2(-100f, 30f), defaultFont);
            var (lightningHighlight, lightningCd) = CreateSpellSlot(spellBarGo.transform, "Slot_Lightning", "[3/F] ⚡ RAIO", new Vector2(100f, 30f), defaultFont);
            var (areaHighlight, areaCd) = CreateSpellSlot(spellBarGo.transform, "Slot_Area", "[4/C] 🌀 ÁREA", new Vector2(300f, 30f), defaultFont);

            // Bottom Bar Controls Hint
            GameObject bottomBar = CreatePanel(hudGo.transform, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Color(0.05f, 0.05f, 0.08f, 0.85f));
            var botBarRect = bottomBar.GetComponent<RectTransform>();
            botBarRect.pivot = new Vector2(0.5f, 0f);
            botBarRect.sizeDelta = new Vector2(0f, 40f);
            botBarRect.anchoredPosition = Vector2.zero;
            var hintText = CreateUIText(bottomBar.transform, "ControlsHint", "[LMB] Disparo  |  [RMB] Feitiço Especial  |  [1..4 / Q,E,F,C / Scroll] Selecionar Feitiço  |  [R] Mana  |  [ESC] Pausar", 17, new Color(0.88f, 0.88f, 0.88f), new Vector2(0f, 20f), new Vector2(0.5f, 0f), defaultFont, TextAnchor.MiddleCenter);

            // --- RETÍCULO DE MIRA DE ALTA PRECISÃO (P.O.V) ---
            GameObject crosshairUIGo = new GameObject("Crosshair_Aim");
            crosshairUIGo.transform.SetParent(hudGo.transform, false);
            var crossRect = crosshairUIGo.AddComponent<RectTransform>();
            crossRect.anchorMin = Vector2.zero;
            crossRect.anchorMax = Vector2.zero;
            crossRect.pivot = new Vector2(0.5f, 0.5f);
            crossRect.sizeDelta = new Vector2(56f, 56f);
            var crossImg = crosshairUIGo.AddComponent<Image>();
            crossImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/crosshair_reticle.png");
            crossImg.raycastTarget = false;

            // Vinculação dos componentes de mira e arma ao WeaponSystem
            SetPrivateField(weaponSystem, "wandRect", wandRect);
            SetPrivateField(weaponSystem, "crosshairRect", crossRect);
            SetPrivateField(weaponSystem, "crosshairImage", crossImg);
            SetPrivateField(weaponSystem, "crystalGlowImage", glowImg);

            // --- MENU INICIAL ---
            GameObject startMenu = CreatePanel(canvasGo.transform, "StartMenuPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0.04f, 0.04f, 0.08f, 0.95f));

            CreateUIText(startMenu.transform, "Title", "CACADA AOS GOBLINS", 56, Color.yellow, new Vector2(0f, 260f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);
            CreateUIText(startMenu.transform, "Subtitle", "Fase 1: Fortaleza Sob a Lua Cheia", 24, Color.white, new Vector2(0f, 195f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            var playBtnObj = CreateButton(startMenu.transform, "PlayButton", "JOGAR AGORA", new Vector2(0f, 60f), new Vector2(0.5f, 0.5f), new Vector2(320f, 65f), defaultFont, new Color(0.5f, 0.1f, 0.8f));
            var playBtn = playBtnObj.GetComponent<Button>();

            var instrBtnObj = CreateButton(startMenu.transform, "InstructionsButton", "GUIA DOS GOBLINS & REGRAS", new Vector2(0f, -20f), new Vector2(0.5f, 0.5f), new Vector2(320f, 55f), defaultFont, new Color(0.15f, 0.3f, 0.6f));
            var instrBtn = instrBtnObj.GetComponent<Button>();

            var quitBtnObj = CreateButton(startMenu.transform, "QuitButton", "SAIR DO JOGO", new Vector2(0f, -95f), new Vector2(0.5f, 0.5f), new Vector2(320f, 50f), defaultFont, new Color(0.35f, 0.1f, 0.1f));
            var quitBtn = quitBtnObj.GetComponent<Button>();

            // Sliders de volume no Menu Inicial
            var (startBgmSlider, startBgmText) = CreateVolumeSlider(startMenu.transform, "StartBgmSlider", "MÚSICA: 28%", new Vector2(0f, -165f), new Vector2(340f, 36f), defaultFont);
            var (startSfxSlider, startSfxText) = CreateVolumeSlider(startMenu.transform, "StartSfxSlider", "EFEITOS: 75%", new Vector2(0f, -215f), new Vector2(340f, 36f), defaultFont);

            // --- PAINEL DE INSTRUÇÕES (COM OS 4 GOBLINS E ARSENAL MÁGICO) ---
            GameObject instrPanel = CreatePanel(startMenu.transform, "InstructionsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.06f, 0.08f, 0.15f, 0.98f));
            var instrRect = instrPanel.GetComponent<RectTransform>();
            instrRect.sizeDelta = new Vector2(980f, 690f);
            instrRect.anchoredPosition = Vector2.zero;

            CreateUIText(instrPanel.transform, "InstrTitle", "MANUAL DOS GOBLINS & ARSENAL MÁGICO", 28, Color.yellow, new Vector2(0f, 305f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            string instrBody = "INIMIGOS (GUIA OFICIAL):\n" +
                               "  • GOBLIN COMUM: 1 HP | 1 Ponto | Move-se atacando com facas.\n" +
                               "  • GOBLIN FUGITIVO: 2 HP | 2 Pontos | Ao sofrer dano, salta em disparada veloz!\n" +
                               "  • GOBLIN DOURADO: 3 HP | 5 Pontos | Mais rapido e agressivo com escudo dourado.\n" +
                               "  • GOBLIN FANTASMA: 4 HP | 3 Pontos | Teleporta atraves de portais misticos ao tomar dano.\n\n" +
                               "ARSENAL MAGICO (CAIXA DE FERRAMENTAS DO MAGO):\n" +
                               "  • 🔥 [LMB] TIRO NORMAL: 1 Dano basico (consome mana da varinha).\n" +
                               "  • 💥 [1 / Q] TIRO ARCANO: 3 Dano concentrado | Cooldown 3.0s.\n" +
                               "  • ❄️ [2 / E] FEITICO DE GELO: 1 Dano + Congela por 2.5s (pausa movimento e fuga) | Cooldown 4.0s.\n" +
                               "  • ⚡ [3 / F] RELAMPAGO: 2 Dano no alvo + 1 Dano eletrico em ate 2 goblins proximos | Cooldown 4.5s.\n" +
                               "  • 🌀 [4 / C] FEITICO DE AREA: Explosao radial (2.5m) causando 2 Dano em todos os inimigos | Cooldown 5.5s.\n" +
                               "  • [RMB]: Dispara o feitico especial ativo  |  [Roda do Mouse / 1..4]: Alterna o feitico selecionado.\n\n" +
                               "SISTEMA DE COMBOS & HEADSHOT:\n" +
                               "  • Multiplicadores: 1-4 (x1) | 5-9 (x2) | 10-19 (x3) | 20+ (x4)!\n" +
                               "  • Errar tiro no vazio ou deixar goblin escapar quebra o combo.\n" +
                               "  • HEADSHOT: Acertos no topo da cabeca concedem +1 Ponto Imediato!\n\n" +
                               "CONTROLES: [R] Recarregar Mana  |  [ESC / P] Pausar o Jogo";
            CreateUIText(instrPanel.transform, "InstrBody", instrBody, 15, Color.white, new Vector2(0f, 15f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleLeft);

            var closeInstrBtnObj = CreateButton(instrPanel.transform, "CloseInstrBtn", "ENTENDIDO! VOLTAR", new Vector2(0f, -305f), new Vector2(0.5f, 0.5f), new Vector2(260f, 48f), defaultFont, new Color(0.2f, 0.5f, 0.2f));
            var closeInstrBtn = closeInstrBtnObj.GetComponent<Button>();
            instrPanel.SetActive(false);

            // --- MENU DE PAUSA ---
            GameObject pausePanel = CreatePanel(canvasGo.transform, "PauseMenuPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.05f, 0.05f, 0.1f, 0.95f));
            var pauseRect = pausePanel.GetComponent<RectTransform>();
            pauseRect.sizeDelta = new Vector2(460f, 480f);
            pauseRect.anchoredPosition = Vector2.zero;

            CreateUIText(pausePanel.transform, "PauseTitle", "JOGO PAUSADO", 40, Color.white, new Vector2(0f, 180f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            var resumeBtnObj = CreateButton(pausePanel.transform, "ResumeBtn", "CONTINUAR", new Vector2(0f, 105f), new Vector2(0.5f, 0.5f), new Vector2(280f, 52f), defaultFont, new Color(0.2f, 0.6f, 0.2f));
            var resumeBtn = resumeBtnObj.GetComponent<Button>();

            var restartBtnObj = CreateButton(pausePanel.transform, "RestartBtn", "REINICIAR", new Vector2(0f, 45f), new Vector2(0.5f, 0.5f), new Vector2(280f, 48f), defaultFont, new Color(0.2f, 0.35f, 0.6f));
            var restartBtn = restartBtnObj.GetComponent<Button>();

            var pauseMenuBtnObj = CreateButton(pausePanel.transform, "PauseMenuBtn", "MENU PRINCIPAL", new Vector2(0f, -15f), new Vector2(0.5f, 0.5f), new Vector2(280f, 48f), defaultFont, new Color(0.4f, 0.15f, 0.15f));
            var pauseMenuBtn = pauseMenuBtnObj.GetComponent<Button>();

            // Sliders de volume no Menu de Pausa
            var (pauseBgmSlider, pauseBgmText) = CreateVolumeSlider(pausePanel.transform, "PauseBgmSlider", "MÚSICA: 28%", new Vector2(0f, -95f), new Vector2(340f, 36f), defaultFont);
            var (pauseSfxSlider, pauseSfxText) = CreateVolumeSlider(pausePanel.transform, "PauseSfxSlider", "EFEITOS: 75%", new Vector2(0f, -150f), new Vector2(340f, 36f), defaultFont);
            pausePanel.SetActive(false);

            // --- MENU DE FIM DE JOGO ---
            GameObject endPanel = CreatePanel(canvasGo.transform, "GameOverPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.04f, 0.04f, 0.08f, 0.96f));
            var endRect = endPanel.GetComponent<RectTransform>();
            endRect.sizeDelta = new Vector2(650f, 460f);
            endRect.anchoredPosition = Vector2.zero;

            var endTitleText = CreateUIText(endPanel.transform, "EndTitle", "VITORIA!", 46, Color.yellow, new Vector2(0f, 160f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);
            var endScoreText = CreateUIText(endPanel.transform, "EndScore", "Pontos Totais: 50", 24, Color.white, new Vector2(0f, 95f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);
            var endStatsText = CreateUIText(endPanel.transform, "EndStats", "Maior Combo: 12  |  Headshots: 5", 20, Color.cyan, new Vector2(0f, 25f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            var endRestartBtnObj = CreateButton(endPanel.transform, "EndRestartBtn", "JOGAR NOVAMENTE", new Vector2(0f, -65f), new Vector2(0.5f, 0.5f), new Vector2(300f, 55f), defaultFont, new Color(0.5f, 0.1f, 0.8f));
            var endRestartBtn = endRestartBtnObj.GetComponent<Button>();

            var endMenuBtnObj = CreateButton(endPanel.transform, "EndMenuBtn", "MENU PRINCIPAL", new Vector2(0f, -135f), new Vector2(0.5f, 0.5f), new Vector2(300f, 50f), defaultFont, new Color(0.25f, 0.25f, 0.35f));
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
            SetPrivateField(uiMgr, "activeSpellNoticeText", strongText);
            SetPrivateField(uiMgr, "controlsHintText", hintText);
            SetPrivateField(uiMgr, "muteButton", muteBtn);
            SetPrivateField(uiMgr, "muteButtonText", muteBtnText);

            SetPrivateField(uiMgr, "spellArsenalContainer", spellBarGo);
            SetPrivateField(uiMgr, "arcaneHighlight", arcaneHighlight);
            SetPrivateField(uiMgr, "arcaneCooldownText", arcaneCd);
            SetPrivateField(uiMgr, "iceHighlight", iceHighlight);
            SetPrivateField(uiMgr, "iceCooldownText", iceCd);
            SetPrivateField(uiMgr, "lightningHighlight", lightningHighlight);
            SetPrivateField(uiMgr, "lightningCooldownText", lightningCd);
            SetPrivateField(uiMgr, "areaHighlight", areaHighlight);
            SetPrivateField(uiMgr, "areaCooldownText", areaCd);

            SetPrivateField(uiMgr, "comboContainer", comboContainer);
            SetPrivateField(uiMgr, "comboText", comboText);
            SetPrivateField(uiMgr, "comboBreakText", comboBreakText);

            SetPrivateField(uiMgr, "headshotPopupContainer", headshotContainer);
            SetPrivateField(uiMgr, "headshotPopupText", headshotText);

            SetPrivateField(uiMgr, "waveText", waveText);
            SetPrivateField(uiMgr, "waveBannerContainer", waveBannerContainer);
            SetPrivateField(uiMgr, "waveBannerTitle", wbTitle);
            SetPrivateField(uiMgr, "waveBannerSubtitle", wbSubtitle);

            SetPrivateField(uiMgr, "endTitleText", endTitleText);
            SetPrivateField(uiMgr, "endScoreText", endScoreText);
            SetPrivateField(uiMgr, "endStatsText", endStatsText);
            SetPrivateField(uiMgr, "restartButton", endRestartBtn);
            SetPrivateField(uiMgr, "endMainMenuButton", endMenuBtn);

            SetPrivateField(uiMgr, "playButton", playBtn);
            SetPrivateField(uiMgr, "instructionsButton", instrBtn);
            SetPrivateField(uiMgr, "closeInstructionsButton", closeInstrBtn);
            SetPrivateField(uiMgr, "quitButton", quitBtn);

            SetPrivateField(uiMgr, "resumeButton", resumeBtn);
            SetPrivateField(uiMgr, "pauseRestartButton", restartBtn);
            SetPrivateField(uiMgr, "pauseMainMenuButton", pauseMenuBtn);

            SetPrivateField(uiMgr, "startBgmSlider", startBgmSlider);
            SetPrivateField(uiMgr, "startBgmText", startBgmText);
            SetPrivateField(uiMgr, "startSfxSlider", startSfxSlider);
            SetPrivateField(uiMgr, "startSfxText", startSfxText);

            SetPrivateField(uiMgr, "pauseBgmSlider", pauseBgmSlider);
            SetPrivateField(uiMgr, "pauseBgmText", pauseBgmText);
            SetPrivateField(uiMgr, "pauseSfxSlider", pauseSfxSlider);
            SetPrivateField(uiMgr, "pauseSfxText", pauseSfxText);

            // 10. GameManager Wiring
            GameObject gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.PopulateDefaultWaves();
            SetPrivateField(gm, "spawner", spawner);
            SetPrivateField(gm, "inputHandler", inputHandler);
            SetPrivateField(gm, "weaponSystem", weaponSystem);
            SetPrivateField(gm, "uiManager", uiMgr);

            // 11. SpellEffectsManager (VFX de Gelo, Raio e Explosão de Área)
            GameObject vfxGo = new GameObject("SpellEffectsManager");
            vfxGo.AddComponent<SpellEffectsManager>();

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
            rect.sizeDelta = new Vector2(750f, 60f);

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

        private static (Slider, Text) CreateVolumeSlider(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, Font font)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = size;
            rootRect.anchoredPosition = anchoredPos;

            // Label Text
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(root.transform, false);
            var tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 0.5f);
            tRect.anchorMax = new Vector2(0f, 0.5f);
            tRect.pivot = new Vector2(0f, 0.5f);
            tRect.anchoredPosition = new Vector2(0f, 0f);
            tRect.sizeDelta = new Vector2(130f, 30f);

            var textComp = textGo.AddComponent<Text>();
            textComp.text = label;
            textComp.font = font;
            textComp.fontSize = 16;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleLeft;

            // Slider GameObject
            var sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(root.transform, false);
            var sRect = sliderGo.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0.5f);
            sRect.anchorMax = new Vector2(1f, 0.5f);
            sRect.pivot = new Vector2(0.5f, 0.5f);
            sRect.anchoredPosition = new Vector2(70f, 0f);
            sRect.sizeDelta = new Vector2(-140f, 22f);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;

            // Background track
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(sliderGo.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var faRect = fillAreaGo.AddComponent<RectTransform>();
            faRect.anchorMin = new Vector2(0f, 0.25f);
            faRect.anchorMax = new Vector2(1f, 0.75f);
            faRect.offsetMin = Vector2.zero;
            faRect.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fRect = fillGo.AddComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = Vector2.zero;
            fRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.75f, 0.35f, 1f);

            // Handle Slide Area
            var handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var haRect = handleAreaGo.AddComponent<RectTransform>();
            haRect.anchorMin = Vector2.zero;
            haRect.anchorMax = Vector2.one;
            haRect.offsetMin = Vector2.zero;
            haRect.offsetMax = Vector2.zero;

            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var hRect = handleGo.AddComponent<RectTransform>();
            hRect.sizeDelta = new Vector2(20f, 20f);
            var hImg = handleGo.AddComponent<Image>();
            hImg.color = Color.white;

            slider.fillRect = fRect;
            slider.handleRect = hRect;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;

            return (slider, textComp);
        }

        private static (Image highlight, Text cooldown) CreateSpellSlot(Transform parent, string name, string hotkeyAndName, Vector2 anchoredPos, Font font)
        {
            GameObject slotGo = CreatePanel(parent, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Color(0.12f, 0.14f, 0.22f, 0.95f));
            var slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(190f, 50f);
            slotRect.anchoredPosition = anchoredPos;

            var highlightImg = slotGo.GetComponent<Image>();
            highlightImg.color = new Color(0.25f, 0.25f, 0.35f, 0.65f);

            var nameText = CreateUIText(slotGo.transform, "Name", hotkeyAndName, 15, Color.white, new Vector2(10f, 10f), new Vector2(0f, 0.5f), font, TextAnchor.MiddleLeft);
            var nRect = nameText.GetComponent<RectTransform>();
            nRect.sizeDelta = new Vector2(170f, 22f);

            var cdText = CreateUIText(slotGo.transform, "Cooldown", "PRONTO", 13, new Color(0.2f, 1f, 0.35f), new Vector2(10f, -11f), new Vector2(0f, 0.5f), font, TextAnchor.MiddleLeft);
            var cdRect = cdText.GetComponent<RectTransform>();
            cdRect.sizeDelta = new Vector2(170f, 20f);

            return (highlightImg, cdText);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
#endif