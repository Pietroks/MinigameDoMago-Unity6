using System.Collections.Generic;
using UnityEngine;

namespace WizardGame.Core
{
    /// <summary>
    /// Gerenciador de áudio 2D com múltiplos canais de SFX simultâneos,
    /// trilha sonora em loop, suporte a Mute, sons de Headshot e Quebra de Combo.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Fontes de Áudio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private List<AudioSource> sfxSources = new List<AudioSource>();
        [SerializeField] private int sfxChannelCount = 6;

        [Header("Trilha Sonora")]
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

        [Header("16 Sons Cômicos de Morte")]
        [SerializeField] private List<AudioClip> commonDeathSounds = new List<AudioClip>();

        private bool isMuted = false;
        private int currentSfxIndex = 0;

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

            SetupAudioSources();
        }

        private void Start()
        {
            if (bgmMusicClip != null)
            {
                PlayBGM(bgmMusicClip);
            }
        }

        private void SetupAudioSources()
        {
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.playOnAwake = false;
            bgmSource.volume = 0.45f;

            if (sfxSources == null) sfxSources = new List<AudioSource>();
            while (sfxSources.Count < sfxChannelCount)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 0f;
                src.playOnAwake = false;
                src.volume = 1.0f;
                sfxSources.Add(src);
            }
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            bgmSource.clip = clip;
            if (!isMuted) bgmSource.Play();
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            if (bgmSource != null) bgmSource.mute = isMuted;
            foreach (var src in sfxSources)
            {
                if (src != null) src.mute = isMuted;
            }
        }

        public bool IsMuted => isMuted;

        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null || isMuted || sfxSources.Count == 0) return;

            AudioSource src = sfxSources[currentSfxIndex];
            currentSfxIndex = (currentSfxIndex + 1) % sfxSources.Count;

            src.volume = Mathf.Clamp01(volume);
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
    }
}