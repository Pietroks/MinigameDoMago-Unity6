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

        private const float DEFAULT_BGM_VOL = 0.28f;
        private const float DEFAULT_SFX_VOL = 0.75f;

        [Header("Fontes de Áudio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private List<AudioSource> sfxSources = new List<AudioSource>();
        [SerializeField] private int sfxChannelCount = 8;

        [Header("Trilha Sonora Profissional")]
        public AudioClip menuMusicClip;
        public List<AudioClip> gameplayPlaylist = new List<AudioClip>();

        [Header("Legado / Fallback")]
        public AudioClip bgmMusicClip;

        [Header("Clipes de Combate")]
        public AudioClip normalShotSound;
        public AudioClip strongShotSound;
        public AudioClip teleportSound;
        public AudioClip hitDamageSound;

        [Header("Clipes de Combo e Precisão")]
        public AudioClip headshotSound;
        public AudioClip comboBreakSound;

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

        public float BGMVolume => bgmVolume;
        public float SFXVolume => sfxVolume;
        public bool IsMuted => isMuted;

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
            PlayMenuMusic();
        }

        private void Update()
        {
            // Se estiver no modo playlist de gameplay e a música atual acabou, toca a próxima
            if (isPlayingGameplayPlaylist && !isMuted && bgmSource != null && !bgmSource.isPlaying)
            {
                if (gameplayPlaylist != null && gameplayPlaylist.Count > 0)
                {
                    PlayNextGameplayTrack();
                }
            }
        }

        private void LoadAudioPreferences()
        {
            bgmVolume = PlayerPrefs.GetFloat(PREF_BGM_VOL, DEFAULT_BGM_VOL);
            sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, DEFAULT_SFX_VOL);
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

        public void PlayGameplayMusic()
        {
            isPlayingGameplayPlaylist = true;

            if (gameplayPlaylist == null || gameplayPlaylist.Count == 0)
            {
                if (bgmMusicClip != null)
                {
                    if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
                    fadeCoroutine = StartCoroutine(FadeToClip(bgmMusicClip, loop: true, 0.8f));
                }
                return;
            }

            AudioClip nextClip = gameplayPlaylist[currentGameplayTrackIndex];
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeToClip(nextClip, loop: false, 0.8f));
        }

        public void PlayNextGameplayTrack()
        {
            if (gameplayPlaylist == null || gameplayPlaylist.Count == 0) return;

            currentGameplayTrackIndex = (currentGameplayTrackIndex + 1) % gameplayPlaylist.Count;
            AudioClip nextClip = gameplayPlaylist[currentGameplayTrackIndex];

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeToClip(nextClip, loop: false, 1.2f));
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
            float targetVol = isMuted ? 0f : bgmVolume;

            // Fade Out se já estiver tocando
            if (bgmSource.isPlaying)
            {
                float startVol = bgmSource.volume;
                float halfDuration = duration * 0.5f;
                float elapsed = 0f;

                while (elapsed < halfDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / halfDuration);
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
                    bgmSource.volume = Mathf.Lerp(0f, targetVol, elapsed / halfDuration);
                    yield return null;
                }

                bgmSource.volume = targetVol;
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
        }

        #endregion

        #region Efeitos Sonoros (SFX)

        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || isMuted || sfxSources == null || sfxSources.Count == 0) return;

            AudioSource src = sfxSources[currentSfxIndex];
            currentSfxIndex = (currentSfxIndex + 1) % sfxSources.Count;

            // Multiplica o volume base calibrado pelo modificador da acao
            src.volume = Mathf.Clamp01(sfxVolume * volumeScale);
            src.PlayOneShot(clip);
        }

        public void PlayHeadshotSound()
        {
            if (headshotSound != null)
            {
                PlaySFX(headshotSound, 1.0f);
            }
        }

        public void PlayComboBreakSound()
        {
            if (comboBreakSound != null)
            {
                PlaySFX(comboBreakSound, 0.8f);
            }
        }

        public void PlayRandomDeathSound()
        {
            if (commonDeathSounds == null || commonDeathSounds.Count == 0 || isMuted) return;

            int idx = Random.Range(0, commonDeathSounds.Count);
            AudioClip chosen = commonDeathSounds[idx];
            if (chosen != null)
            {
                PlaySFX(chosen, 1.0f);
            }
        }

        #endregion
    }
}