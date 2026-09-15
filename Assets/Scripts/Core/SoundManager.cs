using System.Collections.Generic;
using UnityEngine;

namespace WizardGame.Core
{
    /// <summary>
    /// Gerenciador de áudio centralizado para trilha de fundo e efeitos sonoros com suporte a Mute.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Fontes de Áudio")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Clipes de Tiro e Combate")]
        public AudioClip normalShotSound;
        public AudioClip strongShotSound;
        public AudioClip teleportSound;

        [Header("Clipes de Fim de Jogo")]
        public AudioClip victorySound;
        public AudioClip defeatSound;

        [Header("16 Sons de Morte Cômicos (Morte Comum)")]
        [SerializeField] private List<AudioClip> commonDeathSounds = new List<AudioClip>();

        private bool isMuted = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

            bgmSource.loop = true;
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
            if (sfxSource != null) sfxSource.mute = isMuted;
        }

        public bool IsMuted => isMuted;

        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null || isMuted || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, volume);
        }

        public void PlayRandomDeathSound()
        {
            if (commonDeathSounds == null || commonDeathSounds.Count == 0 || isMuted) return;
            int idx = Random.Range(0, commonDeathSounds.Count);
            PlaySFX(commonDeathSounds[idx]);
        }
    }
}
