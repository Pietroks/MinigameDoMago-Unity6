using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGame.Core
{
    /// <summary>
    /// Gerenciador de áudio 2D profissional:
    /// - Música dedicada de Menu e Playlist de Gameplay rotativa
    /// - Transições suaves com Crossfade (independente de Time.timeScale)
    /// - Níveis de volume calibrados para evitar saturação (audio estourando)
    /// - Controle e persistência de volume BGM e SFX via PlayerPrefs
    /// - Múltiplos canais de SFX simultâneos e suporte a Mute rápido
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        private const string PREF_BGM_VOL = "Minigame_BGM_Volume";
        private const string PREF_SFX_VOL = "Minigame_SFX_Volume";
        private const string PREF_MUTED = "Minigame_Audio_Muted";
        private const string PREF_MIX_VERSION = "Minigame_Audio_MixVersion";
        private const int CURRENT_MIX_VERSION = 2;

        private const float DEFAULT_BGM_VOL = 0.42f;
        private const float DEFAULT_SFX_VOL = 0.65f;

        [Header("Fontes de Áudio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private List<AudioSource> sfxSources = new List<AudioSource>();
        [SerializeField] private int sfxChannelCount = 8;

        [Header("Trilha Sonora Profissional")]
        public AudioClip menuMusicClip;
        public List<AudioClip> stageDefaultTracks = new List<AudioClip>();
        public List<AudioClip> gameplayPlaylist = new List<AudioClip>();

        [Header("Legado / Fallback")]
        public AudioClip bgmMusicClip;

        [Header("Clipes de Combate")]
        public AudioClip normalShotSound;
        public AudioClip strongShotSound;
        public AudioClip iceCastSound;
        public AudioClip lightningCastSound;
        public AudioClip areaCastSound;
        public AudioClip teleportSound;
        public AudioClip hitDamageSound;

        [Header("Clipes de Combo e Precisão")]
        public AudioClip headshotSound;
        public AudioClip comboBreakSound;
        public AudioClip powerUpPickupSound;

        [Header("Clipes de Fim de Jogo")]
        public AudioClip victorySound;
        public AudioClip defeatSound;

        [Header("Sons Cômicos de Morte")]
        [SerializeField] private List<AudioClip> commonDeathSounds = new List<AudioClip>();

        private float bgmVolume = DEFAULT_BGM_VOL;
        private float sfxVolume = DEFAULT_SFX_VOL;
        private bool isMuted = false;

        private int currentSfxIndex = 0;
        private int currentGameplayTrackIndex = 0;
        private bool isPlayingGameplayPlaylist = false;
        private Coroutine fadeCoroutine;
        private readonly Dictionary<AudioClip, float> clipLastPlayTime = new Dictionary<AudioClip, float>();
        private float lastDeathSoundTime = -10f;
        private AudioClip proceduralPowerUpChime;
        private AudioClip proceduralCoverBlockClip;

        public float BGMVolume => bgmVolume;
        public float SFXVolume => sfxVolume;
        public bool IsMuted => isMuted;
        public bool IsPlayingGameplayPlaylist => isPlayingGameplayPlaylist;

        public event Action<bool> OnMuteChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            LoadAudioPreferences();
            SetupAudioSources();
        }

        private void Start()
        {
            // Evita sobrescrever a música da Fase 1 caso GameManager.Start() tenha iniciado a partida antes (ex: Reiniciar Partida)
            if (!isPlayingGameplayPlaylist && fadeCoroutine == null && (bgmSource == null || !bgmSource.isPlaying))
            {
                PlayMenuMusic();
            }
        }

        private void Update()
        {
            // Se estiver no modo playlist de gameplay e a música atual acabou, toca a próxima
            if (isPlayingGameplayPlaylist && !isMuted && bgmSource != null && !bgmSource.isPlaying && fadeCoroutine == null)
            {
                if (gameplayPlaylist != null && gameplayPlaylist.Count > 0)
                {
                    PlayNextGameplayTrack();
                }
            }
        }

        private void LoadAudioPreferences()
        {
            if (PlayerPrefs.GetInt(PREF_MIX_VERSION, 1) < CURRENT_MIX_VERSION)
            {
                bgmVolume = DEFAULT_BGM_VOL;
                sfxVolume = DEFAULT_SFX_VOL;
                PlayerPrefs.SetFloat(PREF_BGM_VOL, bgmVolume);
                PlayerPrefs.SetFloat(PREF_SFX_VOL, sfxVolume);
                PlayerPrefs.SetInt(PREF_MIX_VERSION, CURRENT_MIX_VERSION);
                PlayerPrefs.Save();
            }
            else
            {
                bgmVolume = PlayerPrefs.GetFloat(PREF_BGM_VOL, DEFAULT_BGM_VOL);
                sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, DEFAULT_SFX_VOL);
            }
            isMuted = PlayerPrefs.GetInt(PREF_MUTED, 0) == 1;
        }

        private void SetupAudioSources()
        {
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = false;
            bgmSource.spatialBlend = 0f;
            bgmSource.playOnAwake = false;
            bgmSource.volume = isMuted ? 0f : bgmVolume;

            if (sfxSources == null) sfxSources = new List<AudioSource>();
            while (sfxSources.Count < sfxChannelCount)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 0f;
                src.playOnAwake = false;
                src.volume = isMuted ? 0f : sfxVolume;
                sfxSources.Add(src);
            }
        }

        #region Trilha Sonora & Crossfade

        public void PlayMenuMusic()
        {
            isPlayingGameplayPlaylist = false;
            AudioClip target = menuMusicClip != null ? menuMusicClip : bgmMusicClip;
            if (target == null) return;

            if (bgmSource.clip == target && bgmSource.isPlaying) return;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeToClip(target, loop: true, 0.8f));
        }

        public void PlayStageMusic(int stageIndexZeroBased, AudioClip explicitDefaultClip = null)
        {
            isPlayingGameplayPlaylist = true;

            AudioClip startClip = explicitDefaultClip;
            if (startClip == null && stageDefaultTracks != null && stageDefaultTracks.Count > 0)
            {
                int safeIdx = Mathf.Clamp(stageIndexZeroBased, 0, stageDefaultTracks.Count - 1);
                startClip = stageDefaultTracks[safeIdx];
            }

            if (startClip == null && gameplayPlaylist != null && gameplayPlaylist.Count > 0)
            {
                int safeIdx = Mathf.Abs(stageIndexZeroBased) % gameplayPlaylist.Count;
                startClip = gameplayPlaylist[safeIdx];
            }

            if (startClip == null)
            {
                startClip = bgmMusicClip;
            }
            if (startClip == null) return;

            // Sincroniza o índice da playlist para que, se a fase demorar e a música padrão terminar,
            // o jogo avance automaticamente para as OUTRAS músicas da playlist!
            if (gameplayPlaylist != null && gameplayPlaylist.Count > 0)
            {
                int foundIdx = gameplayPlaylist.IndexOf(startClip);
                currentGameplayTrackIndex = foundIdx >= 0 ? foundIdx : (Mathf.Abs(stageIndexZeroBased) % gameplayPlaylist.Count);
            }

            bool hasOtherTracks = CountValidPlaylistTracks(excludeClip: startClip) > 0;
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeToClip(startClip, loop: !hasOtherTracks, 0.7f));
        }

        public void StopMusic(float fadeOutDuration = 0.25f)
        {
            isPlayingGameplayPlaylist = false;
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (bgmSource == null || !bgmSource.isPlaying)
            {
                if (bgmSource != null) bgmSource.Stop();
                return;
            }

            if (fadeOutDuration <= 0.01f)
            {
                bgmSource.Stop();
                bgmSource.volume = 0f;
            }
            else
            {
                fadeCoroutine = StartCoroutine(FadeOutAndStopRoutine(fadeOutDuration));
            }
        }

        private IEnumerator FadeOutAndStopRoutine(float duration)
        {
            float startVol = bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            bgmSource.Stop();
            bgmSource.volume = 0f;
            fadeCoroutine = null;
        }

        private int CountValidPlaylistTracks(AudioClip excludeClip)
        {
            if (gameplayPlaylist == null) return 0;
            int count = 0;
            for (int i = 0; i < gameplayPlaylist.Count; i++)
            {
                if (gameplayPlaylist[i] != null && gameplayPlaylist[i] != excludeClip)
                {
                    count++;
                }
            }
            return count;
        }

        public void PlayGameplayMusic()
        {
            PlayStageMusic(0);
        }

        public void PlayNextGameplayTrack()
        {
            if (gameplayPlaylist == null || gameplayPlaylist.Count == 0) return;

            AudioClip nextClip = GetValidGameplayTrack(startFromCurrent: false);
            if (nextClip == null) return;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeToClip(nextClip, loop: false, 1.0f));
        }

        private AudioClip GetValidGameplayTrack(bool startFromCurrent)
        {
            if (gameplayPlaylist == null || gameplayPlaylist.Count == 0) return null;
            int count = gameplayPlaylist.Count;
            int offset = startFromCurrent ? 0 : 1;
            for (int i = 0; i < count; i++)
            {
                int idx = (currentGameplayTrackIndex + offset + i) % count;
                if (gameplayPlaylist[idx] != null)
                {
                    currentGameplayTrackIndex = idx;
                    return gameplayPlaylist[idx];
                }
            }
            return bgmMusicClip;
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            isPlayingGameplayPlaylist = false;
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeToClip(clip, loop: true, 0.5f));
        }

        private IEnumerator FadeToClip(AudioClip newClip, bool loop, float duration)
        {
            // Fade Out se já estiver tocando
            if (bgmSource.isPlaying)
            {
                float startVol = bgmSource.volume;
                float halfDuration = duration * 0.5f;
                float elapsed = 0f;

                while (elapsed < halfDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    bgmSource.volume = isMuted ? 0f : Mathf.Lerp(startVol, 0f, elapsed / halfDuration);
                    yield return null;
                }
            }

            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.loop = loop;
            bgmSource.volume = 0f;

            if (newClip != null)
            {
                bgmSource.Play();
                float halfDuration = duration * 0.5f;
                float elapsed = 0f;

                while (elapsed < halfDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float liveTargetVol = isMuted ? 0f : bgmVolume;
                    bgmSource.volume = Mathf.Lerp(0f, liveTargetVol, elapsed / halfDuration);
                    yield return null;
                }

                bgmSource.volume = isMuted ? 0f : bgmVolume;
            }

            fadeCoroutine = null;
        }

        #endregion

        #region Ajustes de Volume e Mute

        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PREF_BGM_VOL, bgmVolume);
            PlayerPrefs.Save();

            if (!isMuted && bgmSource != null)
            {
                bgmSource.volume = bgmVolume;
            }
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PREF_SFX_VOL, sfxVolume);
            PlayerPrefs.Save();

            if (!isMuted && sfxSources != null)
            {
                foreach (var src in sfxSources)
                {
                    if (src != null) src.volume = sfxVolume;
                }
            }
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            PlayerPrefs.SetInt(PREF_MUTED, isMuted ? 1 : 0);
            PlayerPrefs.Save();

            if (bgmSource != null)
            {
                bgmSource.volume = isMuted ? 0f : bgmVolume;
            }

            foreach (var src in sfxSources)
            {
                if (src != null)
                {
                    src.volume = isMuted ? 0f : sfxVolume;
                }
            }

            OnMuteChanged?.Invoke(isMuted);
        }

        #endregion

        #region Efeitos Sonoros (SFX)

        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            PlaySFXWithCooldown(clip, volumeScale, 0.055f);
        }

        public void PlaySFXWithCooldown(AudioClip clip, float volumeScale = 1f, float minInterval = 0.055f)
        {
            if (clip == null || isMuted || sfxSources == null || sfxSources.Count == 0) return;

            float now = Time.unscaledTime;
            if (clipLastPlayTime.TryGetValue(clip, out float lastTime) && (now - lastTime) < minInterval)
            {
                return;
            }
            clipLastPlayTime[clip] = now;

            // Balanceamento automático por categoria de clipe para mixagem uniforme
            float categoryScale = GetAutoCategoryScale(clip);
            float effectiveScale = Mathf.Clamp01(volumeScale * categoryScale);

            AudioSource src = GetAvailableSfxSource();
            src.volume = sfxVolume;
            src.PlayOneShot(clip, effectiveScale);
        }

        private AudioSource GetAvailableSfxSource()
        {
            for (int i = 0; i < sfxSources.Count; i++)
            {
                if (sfxSources[i] != null && !sfxSources[i].isPlaying)
                {
                    return sfxSources[i];
                }
            }

            AudioSource fallback = sfxSources[currentSfxIndex];
            currentSfxIndex = (currentSfxIndex + 1) % sfxSources.Count;
            return fallback;
        }

        private float GetAutoCategoryScale(AudioClip clip)
        {
            if (clip == normalShotSound) return 0.62f;
            if (clip == headshotSound) return 0.65f;
            if (clip == strongShotSound || clip == lightningCastSound) return 0.78f;
            if (clip == areaCastSound || clip == defeatSound) return 0.80f;
            if (clip == victorySound) return 0.75f;
            if (clip == iceCastSound || clip == teleportSound) return 0.72f;
            if (clip == comboBreakSound) return 0.72f;
            return 0.75f; // Custom death/escape SFX
        }

        public void PlayHeadshotSound()
        {
            if (headshotSound != null)
            {
                PlaySFXWithCooldown(headshotSound, 0.90f, 0.09f);
            }
        }

        public void PlayComboBreakSound()
        {
            if (comboBreakSound != null)
            {
                PlaySFXWithCooldown(comboBreakSound, 0.75f, 0.25f);
            }
        }

        public void PlayDryFireSound()
        {
            if (comboBreakSound != null)
            {
                PlaySFXWithCooldown(comboBreakSound, 0.35f, 0.12f);
            }
        }

        public void PlayRandomDeathSound()
        {
            if (commonDeathSounds == null || commonDeathSounds.Count == 0 || isMuted) return;

            // Evita empilhamento de múltiplos gritos no mesmo instante (ex: Área / Tempestade)
            float now = Time.unscaledTime;
            if ((now - lastDeathSoundTime) < 0.12f) return;
            lastDeathSoundTime = now;

            int idx = UnityEngine.Random.Range(0, commonDeathSounds.Count);
            AudioClip chosen = commonDeathSounds[idx];
            if (chosen != null)
            {
                PlaySFXWithCooldown(chosen, 0.95f, 0.12f);
            }
        }

        public void PlayIceCastSound()
        {
            if (iceCastSound != null) PlaySFX(iceCastSound, 0.9f);
            else if (teleportSound != null) PlaySFX(teleportSound, 0.75f);
        }

        public void PlayLightningCastSound()
        {
            if (lightningCastSound != null) PlaySFX(lightningCastSound, 0.95f);
            else if (strongShotSound != null) PlaySFX(strongShotSound, 0.9f);
        }

        public void PlayAreaCastSound()
        {
            if (areaCastSound != null) PlaySFX(areaCastSound, 1.0f);
            else if (defeatSound != null) PlaySFX(defeatSound, 0.9f);
        }

        /// <summary>
        /// Som mágico cristalino dedicado à coleta de Power-ups (sem usar a música de vitória).
        /// </summary>
        public void PlayPowerUpPickupSound()
        {
            if (powerUpPickupSound != null)
            {
                PlaySFXWithCooldown(powerUpPickupSound, 0.85f, 0.08f);
                return;
            }

            if (proceduralPowerUpChime == null)
            {
                proceduralPowerUpChime = CreateProceduralPowerUpChime();
            }
            PlaySFXWithCooldown(proceduralPowerUpChime, 0.90f, 0.08f);
        }

        /// <summary>
        /// Som curto e seco de feitiço bloqueado por muro/obstáculo do cenário 2.5D.
        /// </summary>
        public void PlayCoverBlockSound()
        {
            if (proceduralCoverBlockClip == null)
            {
                proceduralCoverBlockClip = CreateProceduralCoverBlockClip();
            }
            PlaySFXWithCooldown(proceduralCoverBlockClip, 0.72f, 0.07f);
        }

        private static AudioClip CreateProceduralPowerUpChime()
        {
            const int sampleRate = 44100;
            const float duration = 0.34f;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[totalSamples];

            // Arpejo mágico cristalino curto (C5 -> E5 -> G5 -> C6)
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float noteStep = 0.062f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)sampleRate;
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float localT = t - (n * noteStep);
                    if (localT < 0f) continue;
                    float env = Mathf.Min(1f, localT / 0.008f) * Mathf.Exp(-localT * 11.5f);
                    float freq = notes[n];
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * localT) * 0.65f
                               + Mathf.Sin(2f * Mathf.PI * (freq * 2f) * localT) * 0.25f;
                    sample += wave * env * 0.32f;
                }
                data[i] = Mathf.Clamp(sample, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("ProceduralPowerUpChime", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateProceduralCoverBlockClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.095f;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 42f);
                // Batida seca em pedra/madeira
                float thud = Mathf.Sin(2f * Mathf.PI * (165f - t * 480f) * t) * 0.72f
                           + Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.22f * Mathf.Exp(-t * 70f);
                data[i] = Mathf.Clamp(thud * env * 0.75f, -0.95f, 0.95f);
            }

            AudioClip clip = AudioClip.Create("ProceduralCoverBlock", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}