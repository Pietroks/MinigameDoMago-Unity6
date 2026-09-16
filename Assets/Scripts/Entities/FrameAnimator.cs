using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGame.Entities
{
    public enum AnimationState
    {
        Idle,
        Walk,
        Run,
        Attack,
        Hit,
        Death
    }

    /// <summary>
    /// Animador leve e altamente responsivo por spritesheets/frames.
    /// Gerencia troca de sprites no SpriteRenderer com taxas de quadros (FPS) personalizadas
    /// e suporte a estados ciclicos (Loop) ou unicos (OneShot).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FrameAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Dictionary<AnimationState, Sprite[]> stateFrames = new Dictionary<AnimationState, Sprite[]>();
        private Dictionary<AnimationState, float> stateFps = new Dictionary<AnimationState, float>();
        private Dictionary<AnimationState, bool> stateLoops = new Dictionary<AnimationState, bool>();

        private AnimationState currentState = AnimationState.Idle;
        private Coroutine activeAnimCoroutine;
        private bool isLocked;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        public void RegisterState(AnimationState state, Sprite[] frames, float fps = 8f, bool loop = true)
        {
            if (frames == null || frames.Length == 0) return;
            stateFrames[state] = frames;
            stateFps[state] = fps;
            stateLoops[state] = loop;
        }

        public void Play(AnimationState state, Action onComplete = null)
        {
            if (isLocked) return;
            if (!stateFrames.ContainsKey(state) || stateFrames[state].Length == 0) return;

            if (currentState == state && activeAnimCoroutine != null && stateLoops.ContainsKey(state) && stateLoops[state])
            {
                return; // Ja esta tocando esse loop
            }

            currentState = state;
            if (activeAnimCoroutine != null)
            {
                StopCoroutine(activeAnimCoroutine);
            }

            activeAnimCoroutine = StartCoroutine(AnimationRoutine(state, onComplete));
        }

        public void PlayOneShot(AnimationState state, AnimationState returnToState = AnimationState.Walk, Action onComplete = null)
        {
            if (isLocked) return;
            if (!stateFrames.ContainsKey(state) || stateFrames[state].Length == 0) return;

            currentState = state;
            if (activeAnimCoroutine != null)
            {
                StopCoroutine(activeAnimCoroutine);
            }

            activeAnimCoroutine = StartCoroutine(OneShotRoutine(state, returnToState, onComplete));
        }

        public void LockAnimation(AnimationState state, Action onComplete = null)
        {
            isLocked = false;
            Play(state, onComplete);
            isLocked = true;
        }

        public void UnlockAnimation()
        {
            isLocked = false;
        }

        private IEnumerator AnimationRoutine(AnimationState state, Action onComplete)
        {
            Sprite[] frames = stateFrames[state];
            float delay = 1f / Mathf.Max(1f, stateFps.ContainsKey(state) ? stateFps[state] : 8f);
            bool loop = stateLoops.ContainsKey(state) ? stateLoops[state] : true;
            int frameIndex = 0;

            while (true)
            {
                if (frameIndex < frames.Length && frames[frameIndex] != null)
                {
                    spriteRenderer.sprite = frames[frameIndex];
                }

                yield return new WaitForSeconds(delay);

                frameIndex++;
                if (frameIndex >= frames.Length)
                {
                    if (loop)
                    {
                        frameIndex = 0;
                    }
                    else
                    {
                        onComplete?.Invoke();
                        yield break;
                    }
                }
            }
        }

        private IEnumerator OneShotRoutine(AnimationState state, AnimationState returnState, Action onComplete)
        {
            Sprite[] frames = stateFrames[state];
            float delay = 1f / Mathf.Max(1f, stateFps.ContainsKey(state) ? stateFps[state] : 8f);

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    spriteRenderer.sprite = frames[i];
                }
                yield return new WaitForSeconds(delay);
            }

            onComplete?.Invoke();
            if (!isLocked)
            {
                Play(returnState);
            }
        }

        public void SetFacingDirection(float moveDirectionX)
        {
            if (Mathf.Abs(moveDirectionX) > 0.01f)
            {
                spriteRenderer.flipX = moveDirectionX < 0;
            }
        }

        public AnimationState GetCurrentState() => currentState;
    }
}