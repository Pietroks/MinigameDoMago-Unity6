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
            var wizardDatas = CreateGoblinScriptableObjects();
            var wizardPrefab = CreateGoblinPrefab();
            SetupGameplayScene(wizardDatas, wizardPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Minigame dos Goblins configurado com sucesso!");
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
                    if (importer.filterMode != FilterMode.Point)
                    {
                        importer.filterMode = FilterMode.Point;
                        dirty = true;
                    }
                    if (dirty)
                    {
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

            // 1. Goblin Comum: 1 HP, 1 Ponto
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
            comum.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/walk_0.png");
            comum.idleFrames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/idle.png") };
            comum.walkFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/walk_0.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/walk_1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/walk_2.png")
            };
            comum.attackFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/attack.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/attack_1.png")
            };
            comum.deathFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/death.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/death_1.png")
            };
            comum.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            EditorUtility.SetDirty(comum);
            list.Add(comum);

            // 2. Goblin Fugitivo: 2 HP, 2 Pontos, salta e corre acelerado ao tomar dano
            var fugitivo = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Fugitivo.asset");
            fugitivo.wizardType = WizardType.Fugitivo;
            fugitivo.displayName = "Goblin Fugitivo";
            fugitivo.maxHealth = 2;
            fugitivo.pointsOnDefeat = 2;
            fugitivo.moveSpeed = 3.2f;
            fugitivo.escapeTimeSeconds = 5.5f;
            fugitivo.spawnWeight = 25;
            fugitivo.escapePenalty = 2;
            fugitivo.baseTint = Color.white;
            fugitivo.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/portrait.png");
            fugitivo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/walk_0.png");
            fugitivo.idleFrames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/idle.png") };
            fugitivo.walkFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/walk_0.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/walk_1.png")
            };
            fugitivo.runFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/run_0.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/run_1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/run_2.png")
            };
            fugitivo.specialActionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/jump.png");
            fugitivo.deathFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/death.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/death_1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/death_2.png")
            };
            fugitivo.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");
            fugitivo.customDamageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/Zé-Wilker-Filho-da-puta (mp3cut.net).mp3");
            EditorUtility.SetDirty(fugitivo);
            list.Add(fugitivo);

            // 3. Goblin Dourado: 3 HP, 5 Pontos, rapido e agressivo com escudo
            var dourado = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Dourado.asset");
            dourado.wizardType = WizardType.Dourado;
            dourado.displayName = "Goblin Dourado";
            dourado.maxHealth = 3;
            dourado.pointsOnDefeat = 5;
            dourado.moveSpeed = 3.6f;
            dourado.escapeTimeSeconds = 6.5f;
            dourado.spawnWeight = 10;
            dourado.escapePenalty = 1;
            dourado.baseTint = new Color(1f, 0.95f, 0.4f);
            dourado.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/portrait.png");
            dourado.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/walk_0.png");
            dourado.idleFrames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/idle.png") };
            dourado.walkFrames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/walk_0.png") };
            dourado.runFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/dash_0.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/dash_1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/dash_2.png")
            };
            dourado.attackFrames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/attack.png") };
            dourado.specialActionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/shield.png");
            dourado.deathFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/death.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/death_1.png")
            };
            dourado.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            dourado.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(dourado);
            list.Add(dourado);

            // 4. Goblin Fantasma: 4 HP, 3 Pontos, teleporte dimensional instantaneo
            var fantasma = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Fantasma.asset");
            fantasma.wizardType = WizardType.Fantasma;
            fantasma.displayName = "Goblin Fantasma";
            fantasma.maxHealth = 4;
            fantasma.pointsOnDefeat = 3;
            fantasma.moveSpeed = 2.4f;
            fantasma.escapeTimeSeconds = 8.5f;
            fantasma.spawnWeight = 15;
            fantasma.escapePenalty = 1;
            fantasma.baseTint = new Color(0.85f, 0.95f, 1f, 0.9f);
            fantasma.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/portrait.png");
            fantasma.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/walk_0.png");
            fantasma.idleFrames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/idle.png") };
            fantasma.walkFrames = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/float_0.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/float_1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/float_2.png")
            };
            fantasma.attackFrames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/cast.png") };
            fantasma.specialActionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/portal.png");
            fantasma.deathFrames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/death.png") };
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

            var scoreText = CreateUIText(topBar.transform, "ScoreText", "Pontos: 0 / 50", 26, Color.white, new Vector2(25f, -22f), new Vector2(0f, 1f), defaultFont);
            var escapeText = CreateUIText(topBar.transform, "EscapeText", "Escaparam: 0 / 15", 22, new Color(1f, 0.6f, 0.6f), new Vector2(25f, -54f), new Vector2(0f, 1f), defaultFont);

            var ammoText = CreateUIText(topBar.transform, "AmmoText", "MANA: 8 / 8  [R]", 24, Color.cyan, new Vector2(0f, -42f), new Vector2(0.5f, 1f), defaultFont, TextAnchor.MiddleCenter);

            var strongText = CreateUIText(topBar.transform, "StrongShotText", "TIRO FORTE [RMB]: PRONTO!", 24, Color.green, new Vector2(-220f, -42f), new Vector2(1f, 1f), defaultFont, TextAnchor.MiddleRight);

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

            // Bottom Bar Controls Hint
            GameObject bottomBar = CreatePanel(hudGo.transform, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Color(0.05f, 0.05f, 0.08f, 0.75f));
            var botBarRect = bottomBar.GetComponent<RectTransform>();
            botBarRect.pivot = new Vector2(0.5f, 0f);
            botBarRect.sizeDelta = new Vector2(0f, 40f);
            botBarRect.anchoredPosition = Vector2.zero;
            var hintText = CreateUIText(bottomBar.transform, "ControlsHint", "[LMB] Disparo  |  [RMB] Tiro Forte  |  [R] Recarregar Mana  |  [ESC / P] Pausar", 18, new Color(0.85f, 0.85f, 0.85f), new Vector2(0f, 20f), new Vector2(0.5f, 0f), defaultFont, TextAnchor.MiddleCenter);

            // --- MENU INICIAL ---
            GameObject startMenu = CreatePanel(canvasGo.transform, "StartMenuPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0.04f, 0.04f, 0.08f, 0.95f));

            CreateUIText(startMenu.transform, "Title", "CACADA AOS GOBLINS", 56, Color.yellow, new Vector2(0f, 260f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);
            CreateUIText(startMenu.transform, "Subtitle", "Elimine os goblins invasores antes que eles fujam!", 24, Color.white, new Vector2(0f, 195f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            var playBtnObj = CreateButton(startMenu.transform, "PlayButton", "JOGAR AGORA", new Vector2(0f, 60f), new Vector2(0.5f, 0.5f), new Vector2(320f, 65f), defaultFont, new Color(0.5f, 0.1f, 0.8f));
            var playBtn = playBtnObj.GetComponent<Button>();

            var instrBtnObj = CreateButton(startMenu.transform, "InstructionsButton", "GUIA DOS GOBLINS & REGRAS", new Vector2(0f, -20f), new Vector2(0.5f, 0.5f), new Vector2(320f, 55f), defaultFont, new Color(0.15f, 0.3f, 0.6f));
            var instrBtn = instrBtnObj.GetComponent<Button>();

            var quitBtnObj = CreateButton(startMenu.transform, "QuitButton", "SAIR DO JOGO", new Vector2(0f, -95f), new Vector2(0.5f, 0.5f), new Vector2(320f, 50f), defaultFont, new Color(0.35f, 0.1f, 0.1f));
            var quitBtn = quitBtnObj.GetComponent<Button>();

            // --- PAINEL DE INSTRUÇÕES (COM OS 4 GOBLINS) ---
            GameObject instrPanel = CreatePanel(startMenu.transform, "InstructionsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.06f, 0.08f, 0.15f, 0.98f));
            var instrRect = instrPanel.GetComponent<RectTransform>();
            instrRect.sizeDelta = new Vector2(920f, 640f);
            instrRect.anchoredPosition = Vector2.zero;

            CreateUIText(instrPanel.transform, "InstrTitle", "MANUAL DOS GOBLINS & MECANICAS", 30, Color.yellow, new Vector2(0f, 280f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleCenter);

            string instrBody = "INIMIGOS (GUIA OFICIAL):\n" +
                               "  • GOBLIN COMUM: 1 HP | 1 Ponto | Move-se aleatoriamente atacando com facas.\n" +
                               "  • GOBLIN FUGITIVO: 2 HP | 2 Pontos | Ao ser atingido, salta e foge em disparada acelerada!\n" +
                               "  • GOBLIN DOURADO: 3 HP | 5 Pontos | Mais rapido e agressivo com escudo. Vale mais pontos!\n" +
                               "  • GOBLIN FANTASMA: 4 HP | 3 Pontos | Teleporte dimensional instantaneo atraves de portais ao tomar dano.\n\n" +
                               "SISTEMA DE COMBOS & MULTIPLICADORES:\n" +
                               "  • 1-4 abates: x1  |  5-9 abates: x2  |  10-19 abates: x3  |  20+ abates: x4!\n" +
                               "  • Errar um tiro no vazio ou deixar um goblin fugir QUEBRA O COMBO imediatamente!\n\n" +
                               "BONUS DE PRECISAO:\n" +
                               "  • HEADSHOT: Acertos no topo da cabeca concedem +1 Ponto Imediato!\n\n" +
                               "COMANDOS:\n" +
                               "  • [LMB]: Disparo  |  [RMB]: Tiro Forte (Dano 3 - Recarga 3s)\n" +
                               "  • [R]: Recarregar Mana (Pente de 8)  |  [ESC / P]: Pausar";
            CreateUIText(instrPanel.transform, "InstrBody", instrBody, 17, Color.white, new Vector2(0f, 25f), new Vector2(0.5f, 0.5f), defaultFont, TextAnchor.MiddleLeft);

            var closeInstrBtnObj = CreateButton(instrPanel.transform, "CloseInstrBtn", "ENTENDIDO! VOLTAR", new Vector2(0f, -270f), new Vector2(0.5f, 0.5f), new Vector2(260f, 48f), defaultFont, new Color(0.2f, 0.5f, 0.2f));
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
            SetPrivateField(uiMgr, "controlsHintText", hintText);
            SetPrivateField(uiMgr, "muteButton", muteBtn);
            SetPrivateField(uiMgr, "muteButtonText", muteBtnText);

            SetPrivateField(uiMgr, "comboContainer", comboContainer);
            SetPrivateField(uiMgr, "comboText", comboText);
            SetPrivateField(uiMgr, "comboBreakText", comboBreakText);

            SetPrivateField(uiMgr, "headshotPopupContainer", headshotContainer);
            SetPrivateField(uiMgr, "headshotPopupText", headshotText);

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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
#endif