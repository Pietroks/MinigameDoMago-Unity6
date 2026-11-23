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
        [MenuItem("Tools/Setup Arcano Complete")]
        public static void SetupProject()
        {
            Debug.Log("Iniciando reconstrucao completa de Arcano: Cacada aos Goblins...");

            ConfigureSprites();
            ConfigureAudio();
            var wizardDatas = CreateGoblinScriptableObjects();
            var wizardPrefab = CreateGoblinPrefab();
            SetupGameplayScene(wizardDatas, wizardPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Arcano: Cacada aos Goblins configurado com sucesso!");
        }

        [MenuItem("Tools/Build Game Standalone")]
        public static void BuildGame()
        {
            Debug.Log("Iniciando reconstrucao e build de Arcano: Cacada aos Goblins...");
            SetupProject();

            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene("Assets/Scenes/Gameplay.unity", true)
            };

            string buildFolder = "Builds";
            if (!System.IO.Directory.Exists(buildFolder))
            {
                System.IO.Directory.CreateDirectory(buildFolder);
            }

            string buildPath = buildFolder + "/ArcanoCacadaAosGoblins.exe";

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
                    bool isSmooth = path.Contains("Background") || path.Contains("cenario") || path.Contains("Weapon") || path.Contains("crosshair") || path.Contains("UI") || path.Contains("Environment");
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

                    // Configurar 9-slice borders para os sprites ornamentados de tema UI
                    Vector4 desiredBorder = Vector4.zero;
                    if (path.EndsWith("PanelFrame_Ornate.png")) desiredBorder = new Vector4(26, 26, 26, 26);
                    else if (path.EndsWith("Button_Gold.png") || path.EndsWith("Button_Stone.png")) desiredBorder = new Vector4(28, 20, 28, 20);
                    else if (path.EndsWith("SpellSlot_Frame.png")) desiredBorder = new Vector4(16, 16, 16, 16);

                    if (desiredBorder != Vector4.zero && importer.spriteBorder != desiredBorder)
                    {
                        importer.spriteBorder = desiredBorder;
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
            comum.escapeTimeSeconds = 6.0f;
            comum.spawnWeight = 50;
            comum.escapePenalty = 1;
            comum.baseTint = Color.white;
            comum.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/portrait.png");
            comum.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Comum/idle_0.png");

            comum.idleFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "idle", 8);
            comum.walkFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "walk", 8);
            comum.attackFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "attack", 2);
            comum.hitFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "hit", 4);
            comum.deathFrames = LoadFrames("Assets/Sprites/Goblins/Comum", "death", 6);
            comum.personalityArchetype = AIPersonalityArchetype.CommonWanderer;
            comum.ApplyArchetypeDefaults();

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
            fugitivo.escapeTimeSeconds = 6.8f;
            fugitivo.spawnWeight = 25;
            fugitivo.escapePenalty = 1;
            fugitivo.baseTint = Color.white;
            fugitivo.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/portrait.png");
            fugitivo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/idle_0.png");
            fugitivo.specialActionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fugitivo/jump.png");
            fugitivo.idleFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "idle", 8);
            fugitivo.walkFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "walk", 8);
            fugitivo.hitFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "hit", 4);
            fugitivo.runFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "run", 8);
            fugitivo.deathFrames = LoadFrames("Assets/Sprites/Goblins/Fugitivo", "death", 6);
            fugitivo.personalityArchetype = AIPersonalityArchetype.FastFleeAfterHit;
            fugitivo.ApplyArchetypeDefaults();
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
            dourado.moveSpeed = 3.0f;
            dourado.escapeTimeSeconds = 8.5f;
            dourado.spawnWeight = 10;
            dourado.escapePenalty = 1;
            dourado.baseTint = Color.white;
            dourado.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/portrait.png");
            dourado.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/idle_0.png");
            dourado.specialActionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Dourado/shield.png");
            dourado.idleFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "idle", 8);
            dourado.walkFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "walk", 8);
            dourado.hitFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "hit", 4);
            dourado.runFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "run", 8);
            dourado.deathFrames = LoadFrames("Assets/Sprites/Goblins/Dourado", "death", 6);
            dourado.personalityArchetype = AIPersonalityArchetype.GoldenStraight;
            dourado.ApplyArchetypeDefaults();
            dourado.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            dourado.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(dourado);
            list.Add(dourado);

            // 4. Goblin Fantasma: HP 4, Pontos 3, Velocidade 3 (Fiel à spritesheet oficial)
            var fantasma = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Fantasma.asset");
            fantasma.wizardType = WizardType.Fantasma;
            fantasma.displayName = "Goblin Fantasma";
            fantasma.maxHealth = 3;
            fantasma.pointsOnDefeat = 3;
            fantasma.moveSpeed = 2.6f;
            fantasma.escapeTimeSeconds = 9.0f;
            fantasma.spawnWeight = 15;
            fantasma.escapePenalty = 1;
            fantasma.baseTint = Color.white;
            fantasma.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/portrait.png");
            fantasma.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/idle_0.png");
            fantasma.specialActionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Fantasma/portal.png");
            fantasma.idleFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "idle", 8);
            fantasma.walkFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "walk", 8);
            fantasma.hitFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "hit", 4);
            fantasma.teleportFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "teleport", 8);
            fantasma.deathFrames = LoadFrames("Assets/Sprites/Goblins/Fantasma", "death", 6);
            fantasma.personalityArchetype = AIPersonalityArchetype.GhostTeleporter;
            fantasma.ApplyArchetypeDefaults();
            fantasma.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/hihi.mp3");
            fantasma.customDamageSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/dbz-teleport.mp3");
            EditorUtility.SetDirty(fantasma);
            list.Add(fantasma);

            // 5. Goblin Chefe (Fase 1): Grande Goblin (HP 18, Pontos 50)
            var chefe = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Chefe.asset");
            chefe.wizardType = WizardType.Chefe;
            chefe.displayName = "Grande Goblin";
            chefe.maxHealth = 18;
            chefe.pointsOnDefeat = 50;
            chefe.moveSpeed = 2.0f;
            chefe.escapeTimeSeconds = 30.0f;
            chefe.spawnWeight = 5;
            chefe.escapePenalty = 3;
            chefe.baseTint = Color.white;
            chefe.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Chefe/portrait.png");
            chefe.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Chefe/idle_0.png");
            chefe.idleFrames = LoadFrames("Assets/Sprites/Goblins/Chefe", "idle", 8);
            chefe.walkFrames = LoadFrames("Assets/Sprites/Goblins/Chefe", "walk", 8);
            chefe.attackFrames = LoadFrames("Assets/Sprites/Goblins/Chefe", "attack1", 7);
            chefe.attack2Frames = LoadFrames("Assets/Sprites/Goblins/Chefe", "attack2", 8);
            chefe.hitFrames = LoadFrames("Assets/Sprites/Goblins/Chefe", "hit", 4);
            chefe.deathFrames = LoadFrames("Assets/Sprites/Goblins/Chefe", "death", 8);
            chefe.personalityArchetype = AIPersonalityArchetype.BossTactical;
            chefe.ApplyArchetypeDefaults();
            chefe.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");
            chefe.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(chefe);
            list.Add(chefe);

            // 6. Goblin Chefe Fase 2: Xamã do Caos (HP 24, Pontos 80)
            var chefe2 = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Chefe2.asset");
            chefe2.wizardType = WizardType.Chefe2;
            chefe2.displayName = "Xamã do Caos";
            chefe2.maxHealth = 24;
            chefe2.pointsOnDefeat = 80;
            chefe2.moveSpeed = 2.2f;
            chefe2.escapeTimeSeconds = 34.0f;
            chefe2.spawnWeight = 5;
            chefe2.escapePenalty = 3;
            chefe2.baseTint = Color.white;
            chefe2.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Chefe2/portrait.png");
            chefe2.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Chefe2/idle_0.png");
            chefe2.idleFrames = LoadFrames("Assets/Sprites/Goblins/Chefe2", "idle", 8);
            chefe2.walkFrames = LoadFrames("Assets/Sprites/Goblins/Chefe2", "walk", 8);
            chefe2.attackFrames = LoadFrames("Assets/Sprites/Goblins/Chefe2", "attack1", 8);
            chefe2.attack2Frames = LoadFrames("Assets/Sprites/Goblins/Chefe2", "attack2", 8);
            chefe2.hitFrames = LoadFrames("Assets/Sprites/Goblins/Chefe2", "hit", 4);
            chefe2.deathFrames = LoadFrames("Assets/Sprites/Goblins/Chefe2", "death", 8);
            chefe2.personalityArchetype = AIPersonalityArchetype.BossTactical;
            chefe2.ApplyArchetypeDefaults();
            chefe2.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");
            chefe2.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(chefe2);
            list.Add(chefe2);

            // 7. Goblin Chefe Fase 3: Feiticeiro Espectral (HP 32, Pontos 120)
            var chefe3 = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Chefe3.asset");
            chefe3.wizardType = WizardType.Chefe3;
            chefe3.displayName = "Feiticeiro Espectral";
            chefe3.maxHealth = 32;
            chefe3.pointsOnDefeat = 120;
            chefe3.moveSpeed = 2.3f;
            chefe3.escapeTimeSeconds = 38.0f;
            chefe3.spawnWeight = 5;
            chefe3.escapePenalty = 3;
            chefe3.baseTint = Color.white;
            chefe3.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Chefe3/portrait.png");
            chefe3.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Chefe3/idle_0.png");
            chefe3.idleFrames = LoadFrames("Assets/Sprites/Goblins/Chefe3", "idle", 8);
            chefe3.walkFrames = LoadFrames("Assets/Sprites/Goblins/Chefe3", "walk", 8);
            chefe3.attackFrames = LoadFrames("Assets/Sprites/Goblins/Chefe3", "attack1", 8);
            chefe3.summonFrames = LoadFrames("Assets/Sprites/Goblins/Chefe3", "summon", 8);
            chefe3.teleportFrames = LoadFrames("Assets/Sprites/Goblins/Chefe3", "teleport", 8);
            chefe3.hitFrames = LoadFrames("Assets/Sprites/Goblins/Chefe3", "hit", 4);
            chefe3.deathFrames = LoadFrames("Assets/Sprites/Goblins/Chefe3", "death", 8);
            chefe3.personalityArchetype = AIPersonalityArchetype.BossTactical;
            chefe3.ApplyArchetypeDefaults();
            chefe3.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");
            chefe3.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(chefe3);
            list.Add(chefe3);

            // 8. Goblin Chefe Fase 4: Lorde Carmesim (HP 42, Pontos 180)
            var chefe4 = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_Chefe4.asset");
            chefe4.wizardType = WizardType.Chefe4;
            chefe4.displayName = "Lorde Carmesim";
            chefe4.maxHealth = 42;
            chefe4.pointsOnDefeat = 180;
            chefe4.moveSpeed = 2.4f;
            chefe4.escapeTimeSeconds = 42.0f;
            chefe4.spawnWeight = 5;
            chefe4.escapePenalty = 3;
            chefe4.baseTint = Color.white;
            chefe4.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Chefe4/portrait.png");
            chefe4.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/Chefe4/idle_0.png");
            chefe4.idleFrames = LoadFrames("Assets/Sprites/Goblins/Chefe4", "idle", 8);
            chefe4.walkFrames = LoadFrames("Assets/Sprites/Goblins/Chefe4", "walk", 8);
            chefe4.attackFrames = LoadFrames("Assets/Sprites/Goblins/Chefe4", "attack1", 8);
            chefe4.attack2Frames = LoadFrames("Assets/Sprites/Goblins/Chefe4", "attack2", 8);
            chefe4.summonFrames = LoadFrames("Assets/Sprites/Goblins/Chefe4", "summon", 8);
            chefe4.hitFrames = LoadFrames("Assets/Sprites/Goblins/Chefe4", "hit", 8);
            chefe4.deathFrames = LoadFrames("Assets/Sprites/Goblins/Chefe4", "death", 8);
            chefe4.personalityArchetype = AIPersonalityArchetype.BossTactical;
            chefe4.ApplyArchetypeDefaults();
            chefe4.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");
            chefe4.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(chefe4);
            list.Add(chefe4);

            // 9. Goblin Chefe Final (Fase 5): GOBLIN REI SUPREMO (HP 60, Pontos 500)
            var chefeFinal = GetOrCreateSO<WizardDataSO>(folder + "/Goblin_ChefeFinal.asset");
            chefeFinal.wizardType = WizardType.ChefeFinal;
            chefeFinal.displayName = "Goblin Rei Supremo";
            chefeFinal.maxHealth = 60;
            chefeFinal.pointsOnDefeat = 500;
            chefeFinal.moveSpeed = 2.5f;
            chefeFinal.escapeTimeSeconds = 50.0f;
            chefeFinal.spawnWeight = 5;
            chefeFinal.escapePenalty = 4;
            chefeFinal.baseTint = Color.white;
            chefeFinal.portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/ChefeFinal/portrait.png");
            chefeFinal.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Goblins/ChefeFinal/idle_0.png");
            chefeFinal.idleFrames = LoadFrames("Assets/Sprites/Goblins/ChefeFinal", "idle", 8);
            chefeFinal.walkFrames = LoadFrames("Assets/Sprites/Goblins/ChefeFinal", "walk", 8);
            chefeFinal.attackFrames = LoadFrames("Assets/Sprites/Goblins/ChefeFinal", "attack1", 8);
            chefeFinal.attack2Frames = LoadFrames("Assets/Sprites/Goblins/ChefeFinal", "attack2", 8);
            chefeFinal.attack3Frames = LoadFrames("Assets/Sprites/Goblins/ChefeFinal", "attack3", 7);
            chefeFinal.summonFrames = LoadFrames("Assets/Sprites/Goblins/ChefeFinal", "summon", 8);
            chefeFinal.hitFrames = LoadFrames("Assets/Sprites/Goblins/ChefeFinal", "hit", 5);
            chefeFinal.deathFrames = LoadFrames("Assets/Sprites/Goblins/ChefeFinal", "death", 8);
            chefeFinal.personalityArchetype = AIPersonalityArchetype.BossTactical;
            chefeFinal.ApplyArchetypeDefaults();
            chefeFinal.escapeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/the-simpsons-nelsons-haha.mp3");
            chefeFinal.customDeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Special/peppino-angry-scream-ear-rape.mp3");
            EditorUtility.SetDirty(chefeFinal);
            list.Add(chefeFinal);

            return list;
        }

        private static Sprite[] LoadFrames(string folderPath, string prefix, int count)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{folderPath}/{prefix}_{i}.png");
                if (s == null && i == 0)
                {
                    s = AssetDatabase.LoadAssetAtPath<Sprite>($"{folderPath}/{prefix}.png");
                }
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

            // 1.1. Cenário de Fundo + StageEnvironmentManager (5 Fases Dinâmicas)
            var bgStage1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Backgrounds/cenario1.png");
            var bgStage2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Backgrounds/Ruínas Iluminadas pela Lua.png");
            var bgStage3 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Backgrounds/Fortaleza Vulcânica da Fase 3.png");
            var bgStage4 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Backgrounds/Salão do Trono em Ruínas.png");
            var bgStage5 = bgStage4; // Salão do Trono em Ruínas com iluminação vulcânica/carmesim e zoom no trono

            var stageBgSprites = new List<Sprite> { bgStage1, bgStage2, bgStage3, bgStage4, bgStage5 };

            // Obstáculos 2.5D recortados por bioma (goblins passam por trás e pela frente pelo eixo Y)
            var propStage1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Environment/Prop_Stage1_Crates.png");
            var propStage2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Environment/Prop_Stage2_Ruins.png");
            var propStage3 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Environment/Prop_Stage3_Volcanic.png");
            var propStage4 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Environment/Prop_Stage4_ThronePillar.png");
            var propStage5 = propStage4;
            var stagePropSprites = new List<Sprite> { propStage1, propStage2, propStage3, propStage4, propStage5 };

            GameObject bgGo = new GameObject("Background_Stage");
            var bgSr = bgGo.AddComponent<SpriteRenderer>();
            bgSr.sprite = bgStage1;
            bgSr.sortingOrder = -50;
            var bgScaler = bgGo.AddComponent<BackgroundScaler>();
            bgScaler.AdjustScale();

            GameObject stageEnvGo = new GameObject("StageEnvironmentManager");
            var stageEnv = stageEnvGo.AddComponent<StageEnvironmentManager>();
            SetPrivateField(stageEnv, "primaryBackground", bgSr);
            SetPrivateField(stageEnv, "primaryScaler", bgScaler);
            SetPrivateField(stageEnv, "targetCamera", cam);
            SetPrivateField(stageEnv, "stageBackgrounds", stageBgSprites);
            SetPrivateField(stageEnv, "stageCoverProps", stagePropSprites);

            // 2. SoundManager
            GameObject soundGo = new GameObject("SoundManager");
            var soundMgr = soundGo.AddComponent<SoundManager>();
            soundMgr.menuMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Goblin Dungeon Menu.mp3");

            var musicStage1 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Mago na Mira.mp3");
            var musicStage2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Spooky Castle Comedy v2.mp3");
            var musicStage3 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Wizard_s Mishap.mp3");
            var musicStage4 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Sky Funeral.mp3");
            var musicStage5 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Arcane Confrontation.mp3");
            var musicExtra = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Tavern Menu of Doom.mp3");

            var stageDefaultMusic = new List<AudioClip>
            {
                musicStage1,
                musicStage2,
                musicStage3,
                musicStage4,
                musicStage5
            };
            soundMgr.stageDefaultTracks = stageDefaultMusic;
            soundMgr.gameplayPlaylist = new List<AudioClip>
            {
                musicStage1,
                musicStage2,
                musicStage3,
                musicStage4,
                musicStage5,
                musicExtra
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

            Font builtinFallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFallback == null) builtinFallback = Resources.GetBuiltinResource<Font>("Arial.ttf");
            Font titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/FantasyTitle.ttf") ?? builtinFallback;
            Font uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/FantasyUI.ttf") ?? builtinFallback;

            // Sprites ornamentados de Tema Dark Fantasy (hud_modelo / menu_modelo)
            Sprite sprPanelOrnate = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/PanelFrame_Ornate.png");
            Sprite sprBtnGold = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/Button_Gold.png");
            Sprite sprBtnStone = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/Button_Stone.png");
            Sprite sprSpellSlot = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/SpellSlot_Frame.png");
            Sprite sprManaOrb = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/ManaOrb_Glow.png");
            Sprite sprManaCryOn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/ManaCrystal_On.png");
            Sprite sprManaCryOff = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/ManaCrystal_Off.png");
            Sprite sprHeartFull = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/Heart_Full.png");
            Sprite sprHeartEmpty = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/Heart_Empty.png");
            Sprite sprSpellMedallion = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/SpellMedallion_Frame.png");
            Sprite sprMenuVignette = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Theme/MenuVignette_Left.png");

            // --- HUD IN-GAME (ESTILO HUD_MODELO: PLACAS FLUTUANTES SEM BARRAS PRETAS PESADAS) ---
            GameObject hudGo = CreatePanel(canvasGo.transform, "InGameHUD", new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0, 0, 0, 0));
            hudGo.GetComponent<Image>().raycastTarget = false;

            // --- ARMA POV EM PRIMEIRA PESSOA (VARINHA & MÃO - ATRÁS DAS PLACAS DE HUD) ---
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

            // 1. TOPO ESQUERDO: Placa de PONTOS + Placa de VIDA (Corações Rubi) + Badge COMBO
            GameObject scorePlaque = CreateOrnatePanel(hudGo.transform, "ScorePlaque", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -20f), new Vector2(215f, 84f), sprPanelOrnate, new Vector2(0f, 1f), raycastTarget: false);
            CreateUIText(scorePlaque.transform, "ScoreHeader", "PONTOS", 14, new Color(0.82f, 0.72f, 0.56f), new Vector2(0f, -15f), new Vector2(0.5f, 1f), uiFont, TextAnchor.MiddleCenter);
            var scoreText = CreateUIText(scorePlaque.transform, "ScoreText", "00000", 33, new Color(1f, 0.94f, 0.78f), new Vector2(0f, -50f), new Vector2(0.5f, 1f), titleFont, TextAnchor.MiddleCenter);
            scoreText.fontStyle = FontStyle.Bold;

            GameObject healthPlaque = CreateOrnatePanel(hudGo.transform, "HealthPlaque", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(252f, -20f), new Vector2(246f, 54f), sprPanelOrnate, new Vector2(0f, 1f), raycastTarget: false);
            var healthHearts = new List<Image>();
            for (int h = 0; h < 5; h++)
            {
                var hGo = new GameObject($"Heart_{h}");
                hGo.transform.SetParent(healthPlaque.transform, false);
                var hr = hGo.AddComponent<RectTransform>();
                hr.anchorMin = new Vector2(0f, 0.5f);
                hr.anchorMax = new Vector2(0f, 0.5f);
                hr.pivot = new Vector2(0.5f, 0.5f);
                hr.sizeDelta = new Vector2(30f, 30f);
                hr.anchoredPosition = new Vector2(28f + h * 34f, 0f);
                var hImg = hGo.AddComponent<Image>();
                hImg.sprite = sprHeartFull;
                hImg.preserveAspect = true;
                hImg.raycastTarget = false;
                healthHearts.Add(hImg);
            }
            var playerHealthText = CreateUIText(healthPlaque.transform, "PlayerHealthText", "5/5", 17, new Color(0.95f, 0.9f, 0.82f), new Vector2(-16f, 0f), new Vector2(1f, 0.5f), uiFont, TextAnchor.MiddleRight);
            playerHealthText.rectTransform.sizeDelta = new Vector2(65f, 30f);

            // Badge de COMBO no canto superior esquerdo abaixo dos pontos (fiel ao hud_modelo)
            GameObject comboContainer = CreateOrnatePanel(hudGo.transform, "ComboContainer", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -114f), new Vector2(285f, 44f), sprPanelOrnate, new Vector2(0f, 1f), raycastTarget: false);
            var comboText = CreateUIText(comboContainer.transform, "ComboText", "COMBO ×2  •  5 ABATES", 20, new Color(1f, 0.78f, 0.22f), Vector2.zero, new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            comboText.fontStyle = FontStyle.Bold;

            // 2. TOPO CENTRO: Pílula da Fase + Buffs Ativos + Aviso de Combo Quebrado
            GameObject wavePlaque = CreateOrnatePanel(hudGo.transform, "WavePlaque", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(380f, 46f), sprPanelOrnate, new Vector2(0.5f, 1f), raycastTarget: false);
            var waveText = CreateUIText(wavePlaque.transform, "WaveText", "FASE 1/5   •   RESTAM 11/11", 18, new Color(1f, 0.88f, 0.45f), Vector2.zero, new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            waveText.fontStyle = FontStyle.Bold;

            GameObject buffsContainer = CreateOrnatePanel(hudGo.transform, "ActiveBuffsContainer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(540f, 38f), sprPanelOrnate, new Vector2(0.5f, 1f), raycastTarget: false);
            var buffsText = CreateUIText(buffsContainer.transform, "ActiveBuffsText", "FÚRIA: 5s   •   TEMPO: 3s", 16, new Color(0.45f, 0.95f, 1f), Vector2.zero, new Vector2(0.5f, 0.5f), uiFont, TextAnchor.MiddleCenter);
            buffsContainer.SetActive(false);

            var comboBreakText = CreateUIText(hudGo.transform, "ComboBreakText", "COMBO QUEBRADO!", 24, new Color(1f, 0.32f, 0.32f), new Vector2(0f, -124f), new Vector2(0.5f, 1f), titleFont, TextAnchor.MiddleCenter);

            // 3. TOPO DIREITO: Placa de FUGIRAM + Botão Mute compacto
            GameObject escapePlaque = CreateOrnatePanel(hudGo.transform, "EscapePlaque", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -20f), new Vector2(225f, 84f), sprPanelOrnate, new Vector2(1f, 1f), raycastTarget: false);
            CreateUIText(escapePlaque.transform, "EscapeHeader", "GOBLINS FUGIRAM", 13, new Color(0.82f, 0.72f, 0.56f), new Vector2(0f, -15f), new Vector2(0.5f, 1f), uiFont, TextAnchor.MiddleCenter);
            var escapeText = CreateUIText(escapePlaque.transform, "EscapeText", "0 / 15", 30, new Color(0.95f, 0.85f, 0.72f), new Vector2(0f, -50f), new Vector2(0.5f, 1f), titleFont, TextAnchor.MiddleCenter);
            escapeText.fontStyle = FontStyle.Bold;

            var muteBtnObj = CreateThemedButton(hudGo.transform, "MuteButton", "SOM: ATIVO", new Vector2(-266f, -45f), new Vector2(1f, 1f), new Vector2(138f, 44f), uiFont, sprBtnStone, Color.white, 14);
            muteBtnObj.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            var muteBtn = muteBtnObj.GetComponent<Button>();
            var muteBtnText = muteBtnObj.GetComponentInChildren<Text>();

            // 4. POPUP DE BÔNUS & BANNER CENTRAL DE ONDA / CHEFE
            GameObject headshotContainer = CreatePanel(hudGo.transform, "HeadshotPopup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0, 0, 0, 0));
            headshotContainer.GetComponent<Image>().raycastTarget = false;
            var hsRect = headshotContainer.GetComponent<RectTransform>();
            hsRect.sizeDelta = new Vector2(720f, 70f);
            hsRect.anchoredPosition = new Vector2(0f, 130f);
            var headshotText = CreateUIText(headshotContainer.transform, "HeadshotText", "HEADSHOT! +2", 34, new Color(0.32f, 1f, 0.45f), Vector2.zero, new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            headshotText.fontStyle = FontStyle.Bold;

            GameObject waveBannerContainer = CreateOrnatePanel(hudGo.transform, "WaveBanner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 165f), new Vector2(780f, 128f), sprPanelOrnate, new Vector2(0.5f, 0.5f), raycastTarget: false);
            var wbTitle = CreateUIText(waveBannerContainer.transform, "BannerTitle", "FASE 1: MURALHAS DA FORTALEZA", 34, new Color(1f, 0.85f, 0.25f), new Vector2(0f, 22f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            wbTitle.fontStyle = FontStyle.Bold;
            var wbSubtitle = CreateUIText(waveBannerContainer.transform, "BannerSubtitle", "7 Comuns + 3 Fugitivos • CHEFE: GRANDE GOBLIN!", 20, new Color(0.95f, 0.92f, 0.86f), new Vector2(0f, -24f), new Vector2(0.5f, 0.5f), uiFont, TextAnchor.MiddleCenter);
            waveBannerContainer.SetActive(false);

            // 5. RODAPÉ ESQUERDO: ORBE DE MANA + CRISTAIS DE MANA (ESTILO HUD_MODELO)
            GameObject manaPlaque = CreateOrnatePanel(hudGo.transform, "ManaPlaque", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(72f, 24f), new Vector2(365f, 78f), sprPanelOrnate, new Vector2(0f, 0f), raycastTarget: false);
            CreateUIText(manaPlaque.transform, "ManaLabel", "MANA", 13, new Color(0.75f, 0.85f, 1f), new Vector2(56f, -15f), new Vector2(0f, 1f), uiFont, TextAnchor.MiddleLeft);
            var ammoText = CreateUIText(manaPlaque.transform, "AmmoText", "10/10  [R]", 17, new Color(0.55f, 0.92f, 1f), new Vector2(-18f, -15f), new Vector2(1f, 1f), uiFont, TextAnchor.MiddleRight);
            ammoText.rectTransform.sizeDelta = new Vector2(180f, 26f);

            var manaCrystals = new List<Image>();
            for (int c = 0; c < 10; c++)
            {
                var cGo = new GameObject($"ManaCrystal_{c}");
                cGo.transform.SetParent(manaPlaque.transform, false);
                var cr = cGo.AddComponent<RectTransform>();
                cr.anchorMin = new Vector2(0f, 0f);
                cr.anchorMax = new Vector2(0f, 0f);
                cr.pivot = new Vector2(0.5f, 0.5f);
                cr.sizeDelta = new Vector2(24f, 36f);
                cr.anchoredPosition = new Vector2(68f + c * 29f, 28f);
                var cImg = cGo.AddComponent<Image>();
                cImg.sprite = sprManaCryOn;
                cImg.preserveAspect = true;
                cImg.raycastTarget = false;
                manaCrystals.Add(cImg);
            }

            // Orbe de Mana sobreposto à esquerda da placa
            GameObject manaOrbGo = new GameObject("ManaOrb_Icon");
            manaOrbGo.transform.SetParent(hudGo.transform, false);
            var orbRect = manaOrbGo.AddComponent<RectTransform>();
            orbRect.anchorMin = Vector2.zero;
            orbRect.anchorMax = Vector2.zero;
            orbRect.pivot = new Vector2(0.5f, 0.5f);
            orbRect.sizeDelta = new Vector2(98f, 98f);
            orbRect.anchoredPosition = new Vector2(72f, 64f);
            var manaOrbImg = manaOrbGo.AddComponent<Image>();
            manaOrbImg.sprite = sprManaOrb;
            manaOrbImg.preserveAspect = true;
            manaOrbImg.raycastTarget = false;

            // 6. RODAPÉ CENTRO: BARRA COMPACTA DO ARSENAL + DICA SUTIL DE CONTROLES
            GameObject spellBarGo = CreateOrnatePanel(hudGo.transform, "SpellArsenalBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(720f, 62f), sprPanelOrnate, new Vector2(0.5f, 0f), raycastTarget: false);

            var (arcaneHighlight, arcaneCd) = CreateSpellSlot(spellBarGo.transform, "Slot_Arcane", "[1/Q] ARCANO", new Vector2(-264f, 31f), uiFont, sprSpellSlot);
            var (iceHighlight, iceCd) = CreateSpellSlot(spellBarGo.transform, "Slot_Ice", "[2/E] GELO", new Vector2(-88f, 31f), uiFont, sprSpellSlot);
            var (lightningHighlight, lightningCd) = CreateSpellSlot(spellBarGo.transform, "Slot_Lightning", "[3/F] RAIO", new Vector2(88f, 31f), uiFont, sprSpellSlot);
            var (areaHighlight, areaCd) = CreateSpellSlot(spellBarGo.transform, "Slot_Area", "[4/C] ÁREA", new Vector2(264f, 31f), uiFont, sprSpellSlot);

            var hintText = CreateUIText(hudGo.transform, "ControlsHint", "[LMB] Disparo   •   [RMB] Magia   •   [1-4 / Scroll] Trocar   •   [R] Mana   •   [M] Som   •   [ESC] Pausa", 14, new Color(0.88f, 0.86f, 0.8f, 0.78f), new Vector2(0f, 16f), new Vector2(0.5f, 0f), uiFont, TextAnchor.MiddleCenter);

            // 7. RODAPÉ DIREITO: MEDALHÃO DE FEITIÇO ATIVO (ESTILO HUD_MODELO)
            GameObject strongPlaque = CreateOrnatePanel(hudGo.transform, "StrongSpellPlaque", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(260f, 78f), sprPanelOrnate, new Vector2(1f, 0f), raycastTarget: false);
            var activeSpellNoticeText = CreateUIText(strongPlaque.transform, "ActiveSpellNotice", "TIRO ARCANO", 18, new Color(1f, 0.92f, 0.72f), new Vector2(-18f, -18f), new Vector2(1f, 1f), titleFont, TextAnchor.MiddleRight);
            activeSpellNoticeText.fontStyle = FontStyle.Bold;
            activeSpellNoticeText.rectTransform.sizeDelta = new Vector2(190f, 28f);

            var strongText = CreateUIText(strongPlaque.transform, "StrongShotText", "[RMB] PRONTO", 15, new Color(0.38f, 1f, 0.55f), new Vector2(-18f, -49f), new Vector2(1f, 1f), uiFont, TextAnchor.MiddleRight);
            strongText.rectTransform.sizeDelta = new Vector2(190f, 24f);

            GameObject medallionGo = new GameObject("SpellMedallion_Frame");
            medallionGo.transform.SetParent(hudGo.transform, false);
            var medRect = medallionGo.AddComponent<RectTransform>();
            medRect.anchorMin = new Vector2(1f, 0f);
            medRect.anchorMax = new Vector2(1f, 0f);
            medRect.pivot = new Vector2(0.5f, 0.5f);
            medRect.sizeDelta = new Vector2(96f, 96f);
            medRect.anchoredPosition = new Vector2(-276f, 64f);
            var medFrameImg = medallionGo.AddComponent<Image>();
            medFrameImg.sprite = sprSpellMedallion;
            medFrameImg.preserveAspect = true;
            medFrameImg.raycastTarget = false;

            GameObject medGlowGo = new GameObject("SpellMedallion_Core");
            medGlowGo.transform.SetParent(medallionGo.transform, false);
            var mgRect = medGlowGo.AddComponent<RectTransform>();
            mgRect.sizeDelta = new Vector2(56f, 56f);
            var spellMedallionGlow = medGlowGo.AddComponent<Image>();
            spellMedallionGlow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Weapon/crystal_glow.png");
            spellMedallionGlow.color = new Color(0.35f, 0.9f, 1f, 1f);
            spellMedallionGlow.raycastTarget = false;

            // --- RETÍCULO DE MIRA DE ALTA PRECISÃO (P.O.V) ---
            GameObject crosshairUIGo = new GameObject("Crosshair_Aim");
            crosshairUIGo.transform.SetParent(hudGo.transform, false);
            var crossRect = crosshairUIGo.AddComponent<RectTransform>();
            crossRect.anchorMin = Vector2.zero;
            crossRect.anchorMax = Vector2.zero;
            crossRect.pivot = new Vector2(0.5f, 0.5f);
            crossRect.sizeDelta = new Vector2(64f, 64f);
            var crossImg = crosshairUIGo.AddComponent<Image>();
            crossImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/crosshair_reticle.png");
            crossImg.preserveAspect = true;
            crossImg.raycastTarget = false;

            // Vinculação dos componentes de mira e arma ao WeaponSystem
            SetPrivateField(weaponSystem, "wandRect", wandRect);
            SetPrivateField(weaponSystem, "crosshairRect", crossRect);
            SetPrivateField(weaponSystem, "crosshairImage", crossImg);
            SetPrivateField(weaponSystem, "crystalGlowImage", glowImg);

            // --- DAMAGE FLASH OVERLAY (VINHETA VERMELHA DE DANO) ---
            GameObject flashGo = CreatePanel(canvasGo.transform, "DamageFlashOverlay", Vector2.zero, Vector2.one, new Color(0.9f, 0.08f, 0.08f, 0f));
            var flashImg = flashGo.GetComponent<Image>();
            flashImg.raycastTarget = false;
            flashGo.SetActive(false);

            // --- MENU INICIAL (ESTILO MENU_MODELO: CASTELO VISÍVEL AO FUNDO + COLUNA À ESQUERDA) ---
            GameObject startMenu = CreatePanel(canvasGo.transform, "StartMenuPanel", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            startMenu.GetComponent<Image>().raycastTarget = false;

            GameObject leftVignette = new GameObject("MenuVignette_Left");
            leftVignette.transform.SetParent(startMenu.transform, false);
            var lvRect = leftVignette.AddComponent<RectTransform>();
            lvRect.anchorMin = Vector2.zero;
            lvRect.anchorMax = new Vector2(0.70f, 1f);
            lvRect.offsetMin = Vector2.zero;
            lvRect.offsetMax = Vector2.zero;
            var lvImg = leftVignette.AddComponent<Image>();
            lvImg.sprite = sprMenuVignette;
            lvImg.color = Color.white;
            lvImg.raycastTarget = false;

            // Coluna lateral esquerda com o novo título oficial: Arcano: Caçada aos Goblins
            float menuCenterX = 335f;
            var titleMain = CreateUIText(startMenu.transform, "Title", "ARCANO", 82, new Color(1f, 0.68f, 0.14f), new Vector2(menuCenterX, 244f), new Vector2(0f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            titleMain.fontStyle = FontStyle.Bold;
            titleMain.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var titleSub = CreateUIText(startMenu.transform, "TitleSub", "CAÇADA AOS GOBLINS", 25, new Color(0.98f, 0.92f, 0.78f), new Vector2(menuCenterX, 174f), new Vector2(0f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            titleSub.fontStyle = FontStyle.Bold;
            titleSub.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var subtitleTxt = CreateUIText(startMenu.transform, "Subtitle", "5 FASES ÉPICAS  •  DEFESA DO CASTELO", 14, new Color(0.82f, 0.75f, 0.62f), new Vector2(menuCenterX, 130f), new Vector2(0f, 0.5f), uiFont, TextAnchor.MiddleCenter);
            subtitleTxt.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            var playBtnObj = CreateThemedButton(startMenu.transform, "PlayButton", "►   JOGAR", new Vector2(menuCenterX, 46f), new Vector2(0f, 0.5f), new Vector2(340f, 64f), titleFont, sprBtnGold, new Color(1f, 0.96f, 0.88f), 24);
            var playBtn = playBtnObj.GetComponent<Button>();

            var instrBtnObj = CreateThemedButton(startMenu.transform, "InstructionsButton", "INSTRUÇÕES & GUIA", new Vector2(menuCenterX, -28f), new Vector2(0f, 0.5f), new Vector2(340f, 56f), titleFont, sprBtnStone, new Color(0.92f, 0.88f, 0.8f), 19);
            var instrBtn = instrBtnObj.GetComponent<Button>();

            var settingsBtnObj = CreateThemedButton(startMenu.transform, "SettingsButton", "CONFIGURAÇÕES", new Vector2(menuCenterX, -94f), new Vector2(0f, 0.5f), new Vector2(340f, 56f), titleFont, sprBtnStone, new Color(0.92f, 0.88f, 0.8f), 19);
            var settingsBtn = settingsBtnObj.GetComponent<Button>();

            var quitBtnObj = CreateThemedButton(startMenu.transform, "QuitButton", "SAIR DO JOGO", new Vector2(menuCenterX, -160f), new Vector2(0f, 0.5f), new Vector2(340f, 54f), titleFont, sprBtnStone, new Color(0.9f, 0.68f, 0.68f), 18);
            var quitBtn = quitBtnObj.GetComponent<Button>();

            var footerMotto = CreateUIText(startMenu.transform, "FooterMotto", "TESTE SUA PRECISÃO\nE PROTEJA O CASTELO!", 16, new Color(0.82f, 0.76f, 0.66f), new Vector2(menuCenterX, -245f), new Vector2(0f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            footerMotto.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // --- PAINEL DE CONFIGURAÇÕES (ABERTO POR CONFIGURAÇÕES) ---
            GameObject settingsPanel = CreateOrnatePanel(startMenu.transform, "SettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 0f), new Vector2(460f, 340f), sprPanelOrnate, new Vector2(0.5f, 0.5f));
            var setTitle = CreateUIText(settingsPanel.transform, "SettingsTitle", "CONFIGURAÇÕES DE ÁUDIO", 26, new Color(1f, 0.82f, 0.35f), new Vector2(0f, 115f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            setTitle.fontStyle = FontStyle.Bold;
            var (startBgmSlider, startBgmText) = CreateVolumeSlider(settingsPanel.transform, "StartBgmSlider", "MÚSICA: 42%", new Vector2(0f, 35f), new Vector2(370f, 38f), uiFont);
            var (startSfxSlider, startSfxText) = CreateVolumeSlider(settingsPanel.transform, "StartSfxSlider", "EFEITOS: 65%", new Vector2(0f, -25f), new Vector2(370f, 38f), uiFont);
            var closeSettingsBtnObj = CreateThemedButton(settingsPanel.transform, "CloseSettingsBtn", "VOLTAR", new Vector2(0f, -108f), new Vector2(0.5f, 0.5f), new Vector2(220f, 50f), titleFont, sprBtnGold, Color.white, 18);
            var closeSettingsBtn = closeSettingsBtnObj.GetComponent<Button>();
            settingsPanel.SetActive(false);

            // --- PAINEL DE INSTRUÇÕES (ORNO E LEGÍVEL) ---
            GameObject instrPanel = CreateOrnatePanel(startMenu.transform, "InstructionsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020f, 750f), sprPanelOrnate, new Vector2(0.5f, 0.5f));
            var instrTitle = CreateUIText(instrPanel.transform, "InstrTitle", "GRIMÓRIO DO MAGO  •  REGRAS & ARSENAL", 28, new Color(1f, 0.82f, 0.32f), new Vector2(0f, 326f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            instrTitle.fontStyle = FontStyle.Bold;

            string instrBody = "<color=#FFD269><b>INIMIGOS (GUIA OFICIAL DOS GOBLINS):</b></color>\n" +
                               "  • <b>GOBLIN COMUM</b> (1 HP | 1 Pt): Perambula pela arena; não ataca ao escapar.\n" +
                               "  • <b>GOBLIN FUGITIVO</b> (2 HP | 2 Pts): Fica mais veloz a cada fase e dispara ao ser ferido; causa <b>1 de Dano</b> ao fugir.\n" +
                               "  • <b>GOBLIN DOURADO</b> (3 HP | 5 Pts): Escudo reforçado; causa <b>2 de Dano</b> à sua Vida ao fugir.\n" +
                               "  • <b>GOBLIN FANTASMA</b> (3 HP | 3 Pts): Teleporta através de portais ao receber dano.\n\n" +
                               "<color=#FFD269><b>AS 5 FASES & GRANDES CHEFES:</b></color>\n" +
                               "  • <b>FASE 1</b> (Muralhas): Grande Goblin (18 HP)   •   <b>FASE 2</b> (Ruínas): Xamã do Caos (24 HP)\n" +
                               "  • <b>FASE 3</b> (Fortaleza Vulcânica): Feiticeiro Espectral (32 HP)\n" +
                               "  • <b>FASE 4</b> (Salão do Trono): Lorde Carmesim (42 HP)   •   <b>FASE 5</b> (Altar): Goblin Rei Supremo (60 HP)\n" +
                               "  • <b>DEFESA ATIVA:</b> Clique nos Orbes em voo (+5 pts) e acerte Feitiços em golpes carregados (+8 pts CONTRA-ATAQUE)!\n\n" +
                               "<color=#7CE0FF><b>VIDA, MANA & POWER-UPS:</b></color>\n" +
                               "  • Você possui <b>5 Pontos de Vida</b> e <b>10 Cargas de Mana</b>. Cada Chefe derrotado cura <b>+2 Vida</b> e restaura Mana!\n" +
                               "  • Deixar <b>15 Goblins</b> escaparem ou zerar sua Vida encerra a partida.\n" +
                               "  • <b>ORBES RAROS:</b> Mana (+4 tiros)  •  Fúria (+1 Dano 5s)  •  Tempo (50% Lento 3s)  •  Tempestade  •  Mira Sniper (5s).\n\n" +
                               "<color=#FFB469><b>ARSENAL MÁGICO & COMBOS:</b></color>\n" +
                               "  • <b>[LMB] NORMAL:</b> 1 Dano (+1 Crítico em Headshot!)   •   <b>[1/Q] ARCANO:</b> 4 Dano   •   <b>[2/E] GELO:</b> Congela 2.8s\n" +
                               "  • <b>[3/F] RELÂMPAGO:</b> 2 Dano em até 4 alvos   •   <b>[4/C] ÁREA:</b> Explosão 2.8m (3 Dano)\n" +
                               "  • <b>COMBOS & RANK:</b> Multiplicadores ×1 a ×4 (subir combo recupera +2 Mana)! Rank avalia Abates, Precisão, Tempo e Fugas.";
            var instrBodyTxt = CreateUIText(instrPanel.transform, "InstrBody", instrBody, 14, new Color(0.94f, 0.92f, 0.88f), new Vector2(0f, 18f), new Vector2(0.5f, 0.5f), uiFont, TextAnchor.MiddleLeft);
            instrBodyTxt.supportRichText = true;
            instrBodyTxt.lineSpacing = 1.15f;
            instrBodyTxt.rectTransform.sizeDelta = new Vector2(920f, 560f);

            var closeInstrBtnObj = CreateThemedButton(instrPanel.transform, "CloseInstrBtn", "ENTENDIDO! VOLTAR", new Vector2(0f, -315f), new Vector2(0.5f, 0.5f), new Vector2(280f, 52f), titleFont, sprBtnGold, Color.white, 19);
            var closeInstrBtn = closeInstrBtnObj.GetComponent<Button>();
            instrPanel.SetActive(false);

            // --- MENU DE PAUSA ---
            GameObject pausePanel = CreateOrnatePanel(canvasGo.transform, "PauseMenuPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 500f), sprPanelOrnate, new Vector2(0.5f, 0.5f));
            var pauseTitle = CreateUIText(pausePanel.transform, "PauseTitle", "JOGO PAUSADO", 38, new Color(1f, 0.84f, 0.32f), new Vector2(0f, 190f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            pauseTitle.fontStyle = FontStyle.Bold;

            var resumeBtnObj = CreateThemedButton(pausePanel.transform, "ResumeBtn", "CONTINUAR", new Vector2(0f, 108f), new Vector2(0.5f, 0.5f), new Vector2(300f, 54f), titleFont, sprBtnGold, Color.white, 20);
            var resumeBtn = resumeBtnObj.GetComponent<Button>();

            var restartBtnObj = CreateThemedButton(pausePanel.transform, "RestartBtn", "REINICIAR PARTIDA", new Vector2(0f, 44f), new Vector2(0.5f, 0.5f), new Vector2(300f, 50f), titleFont, sprBtnStone, Color.white, 18);
            var restartBtn = restartBtnObj.GetComponent<Button>();

            var pauseMenuBtnObj = CreateThemedButton(pausePanel.transform, "PauseMenuBtn", "MENU PRINCIPAL", new Vector2(0f, -18f), new Vector2(0.5f, 0.5f), new Vector2(300f, 50f), titleFont, sprBtnStone, new Color(0.95f, 0.72f, 0.72f), 18);
            var pauseMenuBtn = pauseMenuBtnObj.GetComponent<Button>();

            var (pauseBgmSlider, pauseBgmText) = CreateVolumeSlider(pausePanel.transform, "PauseBgmSlider", "MÚSICA: 42%", new Vector2(0f, -104f), new Vector2(360f, 38f), uiFont);
            var (pauseSfxSlider, pauseSfxText) = CreateVolumeSlider(pausePanel.transform, "PauseSfxSlider", "EFEITOS: 65%", new Vector2(0f, -158f), new Vector2(360f, 38f), uiFont);
            pausePanel.SetActive(false);

            // --- MENU DE FIM DE JOGO (ESTILO CARTÃO DE RANK) ---
            GameObject endPanel = CreateOrnatePanel(canvasGo.transform, "GameOverPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740f, 690f), sprPanelOrnate, new Vector2(0.5f, 0.5f));

            var endTitleText = CreateUIText(endPanel.transform, "EndTitle", "VITÓRIA", 38, new Color(1f, 0.85f, 0.22f), new Vector2(0f, 282f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            endTitleText.fontStyle = FontStyle.Bold;
            endTitleText.supportRichText = true;
            endTitleText.rectTransform.sizeDelta = new Vector2(680f, 90f);

            var endRankGradeText = CreateUIText(endPanel.transform, "EndRankGrade", "S", 88, new Color(1f, 0.84f, 0f), new Vector2(0f, 176f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            endRankGradeText.fontStyle = FontStyle.Bold;

            var endScoreText = CreateUIText(endPanel.transform, "EndScore", "ARQUIMAGO SUPREMO", 22, Color.white, new Vector2(0f, 102f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            endScoreText.fontStyle = FontStyle.Bold;
            endScoreText.supportRichText = true;

            var endStatsText = CreateUIText(endPanel.transform, "EndStats", "Estatísticas de Desempenho", 18, Color.white, new Vector2(0f, -24f), new Vector2(0.5f, 0.5f), uiFont, TextAnchor.MiddleCenter);
            endStatsText.supportRichText = true;
            endStatsText.rectTransform.sizeDelta = new Vector2(660f, 220f);

            var endRestartBtnObj = CreateThemedButton(endPanel.transform, "EndRestartBtn", "JOGAR NOVAMENTE", new Vector2(0f, -218f), new Vector2(0.5f, 0.5f), new Vector2(320f, 54f), titleFont, sprBtnGold, Color.white, 20);
            var endRestartBtn = endRestartBtnObj.GetComponent<Button>();

            var endMenuBtnObj = CreateThemedButton(endPanel.transform, "EndMenuBtn", "MENU PRINCIPAL", new Vector2(0f, -280f), new Vector2(0.5f, 0.5f), new Vector2(320f, 48f), titleFont, sprBtnStone, Color.white, 18);
            var endMenuBtn = endMenuBtnObj.GetComponent<Button>();
            endPanel.SetActive(false);

            // --- PAINEL DE TRANSIÇÃO DE FASES (CORTINA CINEMATOGRÁFICA) ---
            GameObject stageTransPanel = CreatePanel(canvasGo.transform, "StageTransitionPanel", Vector2.zero, Vector2.one, new Color(0.03f, 0.03f, 0.07f, 0.96f));
            stageTransPanel.GetComponent<Image>().raycastTarget = true;
            var transTitle = CreateUIText(stageTransPanel.transform, "TransitionTitle", "FASE 2", 72, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 88f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            transTitle.fontStyle = FontStyle.Bold;
            var transSubtitle = CreateUIText(stageTransPanel.transform, "TransitionSubtitle", "RUÍNAS ILUMINADAS PELA LUA", 34, Color.white, new Vector2(0f, 6f), new Vector2(0.5f, 0.5f), titleFont, TextAnchor.MiddleCenter);
            transSubtitle.fontStyle = FontStyle.Bold;
            var transDetail = CreateUIText(stageTransPanel.transform, "TransitionDetail", "Chefe: Xamã do Caos", 21, new Color(0.75f, 0.9f, 1f), new Vector2(0f, -82f), new Vector2(0.5f, 0.5f), uiFont, TextAnchor.MiddleCenter);
            transDetail.rectTransform.sizeDelta = new Vector2(1100f, 140f);
            stageTransPanel.SetActive(false);

            // 9. UIManager Wiring
            var uiMgr = canvasGo.AddComponent<UIManager>();
            SetPrivateField(uiMgr, "startMenuPanel", startMenu);
            SetPrivateField(uiMgr, "pauseMenuPanel", pausePanel);
            SetPrivateField(uiMgr, "instructionsPanel", instrPanel);
            SetPrivateField(uiMgr, "settingsPanel", settingsPanel);
            SetPrivateField(uiMgr, "gameOverPanel", endPanel);
            SetPrivateField(uiMgr, "inGameHUD", hudGo);

            SetPrivateField(uiMgr, "scoreText", scoreText);
            SetPrivateField(uiMgr, "escapeText", escapeText);
            SetPrivateField(uiMgr, "playerHealthText", playerHealthText);
            SetPrivateField(uiMgr, "damageFlashOverlay", flashImg);
            SetPrivateField(uiMgr, "ammoText", ammoText);
            SetPrivateField(uiMgr, "strongShotText", strongText);
            SetPrivateField(uiMgr, "activeSpellNoticeText", activeSpellNoticeText);
            SetPrivateField(uiMgr, "controlsHintText", hintText);
            SetPrivateField(uiMgr, "muteButton", muteBtn);
            SetPrivateField(uiMgr, "muteButtonText", muteBtnText);

            SetPrivateField(uiMgr, "healthHeartImages", healthHearts);
            SetPrivateField(uiMgr, "heartFullSprite", sprHeartFull);
            SetPrivateField(uiMgr, "heartEmptySprite", sprHeartEmpty);
            SetPrivateField(uiMgr, "manaCrystalImages", manaCrystals);
            SetPrivateField(uiMgr, "manaCrystalOnSprite", sprManaCryOn);
            SetPrivateField(uiMgr, "manaCrystalOffSprite", sprManaCryOff);
            SetPrivateField(uiMgr, "manaOrbImage", manaOrbImg);
            SetPrivateField(uiMgr, "spellMedallionGlowImage", spellMedallionGlow);

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

            SetPrivateField(uiMgr, "activeBuffsContainer", buffsContainer);
            SetPrivateField(uiMgr, "activeBuffsText", buffsText);

            SetPrivateField(uiMgr, "headshotPopupContainer", headshotContainer);
            SetPrivateField(uiMgr, "headshotPopupText", headshotText);

            SetPrivateField(uiMgr, "waveText", waveText);
            SetPrivateField(uiMgr, "waveBannerContainer", waveBannerContainer);
            SetPrivateField(uiMgr, "waveBannerTitle", wbTitle);
            SetPrivateField(uiMgr, "waveBannerSubtitle", wbSubtitle);
            SetPrivateField(uiMgr, "stageTransitionPanel", stageTransPanel);
            SetPrivateField(uiMgr, "stageTransitionTitleText", transTitle);
            SetPrivateField(uiMgr, "stageTransitionSubtitleText", transSubtitle);
            SetPrivateField(uiMgr, "stageTransitionDetailText", transDetail);

            SetPrivateField(uiMgr, "endTitleText", endTitleText);
            SetPrivateField(uiMgr, "endRankGradeText", endRankGradeText);
            SetPrivateField(uiMgr, "endScoreText", endScoreText);
            SetPrivateField(uiMgr, "endStatsText", endStatsText);
            SetPrivateField(uiMgr, "restartButton", endRestartBtn);
            SetPrivateField(uiMgr, "endMainMenuButton", endMenuBtn);

            SetPrivateField(uiMgr, "playButton", playBtn);
            SetPrivateField(uiMgr, "instructionsButton", instrBtn);
            SetPrivateField(uiMgr, "closeInstructionsButton", closeInstrBtn);
            SetPrivateField(uiMgr, "settingsButton", settingsBtn);
            SetPrivateField(uiMgr, "closeSettingsButton", closeSettingsBtn);
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
            SetPrivateField(gm, "stageBackgroundSprites", stageBgSprites);
            SetPrivateField(gm, "stageDefaultMusicClips", stageDefaultMusic);
            SetPrivateField(gm, "stageEnvironment", stageEnv);
            gm.PopulateDefaultWaves();
            SetPrivateField(gm, "spawner", spawner);
            SetPrivateField(gm, "inputHandler", inputHandler);
            SetPrivateField(gm, "weaponSystem", weaponSystem);
            SetPrivateField(gm, "uiManager", uiMgr);
            SetPrivateField(gm, "maxPlayerHealth", 5);

            // 11. PowerUpManager (Orbes Mágicos Flutuantes Raros)
            GameObject powerUpGo = new GameObject("PowerUpManager");
            var powerUpMgr = powerUpGo.AddComponent<PowerUpManager>();
            SetPrivateField(powerUpMgr, "weaponSystem", weaponSystem);
            SetPrivateField(powerUpMgr, "uiManager", uiMgr);
            SetPrivateField(gm, "powerUpManager", powerUpMgr);

            // 12. SpellEffectsManager (VFX de Gelo, Raio e Explosão de Área)
            GameObject vfxGo = new GameObject("SpellEffectsManager");
            vfxGo.AddComponent<SpellEffectsManager>();

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static GameObject CreateOrnatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Sprite frameSprite, Vector2 pivot, bool raycastTarget = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var img = go.AddComponent<Image>();
            img.raycastTarget = raycastTarget;
            if (frameSprite != null)
            {
                img.sprite = frameSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.06f, 0.06f, 0.11f, 0.94f);
            }
            return go;
        }

        private static GameObject CreateThemedButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 anchor, Vector2 size, Font font, Sprite btnSprite, Color textColor, int fontSize = 20)
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
            if (btnSprite != null)
            {
                img.sprite = btnSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.25f, 0.25f, 0.35f);
            }

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; // UIButtonJuice controla hover/press em unscaledDeltaTime

            go.AddComponent<UIButtonJuice>();

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(18f, 0f);
            tRect.offsetMax = new Vector2(-18f, 0f);

            var t = textGo.AddComponent<Text>();
            t.text = label;
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.color = textColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            return go;
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
            t.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(2f, -3f);

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
            bgImg.color = new Color(0.08f, 0.08f, 0.14f, 0.95f);

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
            fillImg.color = new Color(0.92f, 0.62f, 0.16f, 1f);

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
            hImg.color = new Color(1f, 0.88f, 0.45f, 1f);

            slider.fillRect = fRect;
            slider.handleRect = hRect;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;

            return (slider, textComp);
        }

        private static (Image highlight, Text cooldown) CreateSpellSlot(Transform parent, string name, string hotkeyAndName, Vector2 anchoredPos, Font font, Sprite slotFrame = null)
        {
            GameObject slotGo = CreatePanel(parent, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Color(0.14f, 0.15f, 0.24f, 0.95f));
            var slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(166f, 46f);
            slotRect.anchoredPosition = anchoredPos;

            var highlightImg = slotGo.GetComponent<Image>();
            highlightImg.raycastTarget = false;
            if (slotFrame != null)
            {
                highlightImg.sprite = slotFrame;
                highlightImg.type = Image.Type.Sliced;
            }
            highlightImg.color = new Color(0.45f, 0.38f, 0.3f, 0.72f);

            var nameText = CreateUIText(slotGo.transform, "Name", hotkeyAndName, 14, new Color(0.98f, 0.94f, 0.84f), new Vector2(0f, 9f), new Vector2(0.5f, 0.5f), font, TextAnchor.MiddleCenter);
            nameText.fontStyle = FontStyle.Bold;
            var nRect = nameText.GetComponent<RectTransform>();
            nRect.pivot = new Vector2(0.5f, 0.5f);
            nRect.sizeDelta = new Vector2(154f, 20f);

            var cdText = CreateUIText(slotGo.transform, "Cooldown", "PRONTO", 12, new Color(0.35f, 0.98f, 0.52f), new Vector2(0f, -10f), new Vector2(0.5f, 0.5f), font, TextAnchor.MiddleCenter);
            var cdRect = cdText.GetComponent<RectTransform>();
            cdRect.pivot = new Vector2(0.5f, 0.5f);
            cdRect.sizeDelta = new Vector2(154f, 18f);

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