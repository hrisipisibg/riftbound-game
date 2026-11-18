using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class AudioEffects : MonoBehaviour
    {
        public static AudioEffects Instance { get; private set; }

        AudioSource source;
        readonly AudioSource[] uiVoices = new AudioSource[3];
        readonly float[] nextCue = new float[8];
        readonly List<AudioClip> generatedClips = new List<AudioClip>();
        readonly System.Random variation = new System.Random(9247);
        AudioClip[] coinClips, swingClips, footstepClips, pageClips, clickClips;
        PlayerController player;
        Vector3 previousPosition;
        float strideDistance, worldReadyAt, nextSwing, nextSpell;
        bool worldSuspended;
        int nextVoice;

        public static float EffectsVolume => SettingsSystem.Instance ? Mathf.Clamp01(SettingsSystem.Instance.sfxVolume) : .85f;
        public static bool CanPlayWorld => Instance && Instance.isActiveAndEnabled && !FrontendMenu.IsOpen && Time.timeScale > 0 && Time.unscaledTime >= Instance.worldReadyAt;

        AudioClip sfxLevelUp;
        AudioClip sfxQuestComplete;
        AudioClip sfxQuestAccept;
        AudioClip sfxLootCoin;
        AudioClip sfxBladeHit;
        AudioClip sfxSpellCast;
        AudioClip sfxClick;
        AudioClip sfxZoneDiscover;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D UI sound
            source.dopplerLevel = 0;
            source.priority = 80;
            uiVoices[0] = source;
            for (int i = 1; i < uiVoices.Length; i++)
            {
                uiVoices[i] = gameObject.AddComponent<AudioSource>();
                uiVoices[i].playOnAwake = false;
                uiVoices[i].spatialBlend = 0;
                uiVoices[i].dopplerLevel = 0;
                uiVoices[i].priority = 80;
            }
            player = GetComponent<PlayerController>();
            previousPosition = transform.position;
            worldReadyAt = Time.unscaledTime + .45f;

            GenerateProceduralAudioClips();
        }

        void GenerateProceduralAudioClips()
        {
            sfxClick = LoadRecording("mouseclick1");
            if (!sfxClick) sfxClick = CreateToneClip("Click", .045f, 680f, .18f, ToneType.Sine);
            sfxBladeHit = CreateNoiseHitClip("BladeHit", 0.15f, 0.4f);
            sfxSpellCast = CreateChirpClip("SpellCast", .42f, 220f, 520f, .28f);
            sfxLootCoin = CreateArpeggioClip("LootCoin", 0.25f, new float[] { 880f, 1320f, 1760f }, 0.35f);
            sfxQuestAccept = CreateArpeggioClip("QuestAccept", 0.45f, new float[] { 440f, 554.37f, 659.25f }, 0.4f);
            sfxQuestComplete = CreateArpeggioClip("QuestComplete", 0.7f, new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.5f);
            sfxLevelUp = CreateFanfareClip("LevelUp", 1.1f);
            sfxZoneDiscover = CreateArpeggioClip("ZoneDiscover", 0.85f, new float[] { 392f, 493.88f, 587.33f, 783.99f }, 0.45f);
            coinClips = LoadRecordings("handleCoins", "handleCoins2");
            swingClips = LoadRecordings("knifeSlice", "knifeSlice2");
            footstepClips = LoadRecordings("footstep00", "footstep02", "footstep04", "footstep06");
            pageClips = LoadRecordings("bookOpen", "bookFlip1", "bookClose");
            clickClips = LoadRecordings("mouseclick1", "click1");
        }

        public static AudioClip LoadRecording(string name) => Resources.Load<AudioClip>("RiftboundAudio/" + name);

        static AudioClip[] LoadRecordings(params string[] names)
        {
            var clips = new List<AudioClip>();
            foreach (string name in names)
            {
                var clip = LoadRecording(name);
                if (clip) clips.Add(clip);
            }
            return clips.ToArray();
        }

        AudioClip Pick(AudioClip[] clips, AudioClip fallback) => clips != null && clips.Length > 0 ? clips[variation.Next(clips.Length)] : fallback;

        void Update()
        {
            if (!CanPlayWorld && !worldSuspended) GameSound.StopWorldVoices();
            worldSuspended = !CanPlayWorld;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;
            var position = player.transform.position;
            float moved = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up).magnitude;
            previousPosition = position;
            var motor = player.GetComponent<CharacterController>();
            if (!CanPlayWorld || !player.Stats || !player.Stats.Alive || player.Mounted || player.Mounting || !motor || !motor.isGrounded || moved > 2f || moved < .0001f)
            {
                strideDistance = 0;
                return;
            }
            strideDistance += moved;
            float stride = player.Walking ? .75f : 1.45f;
            if (strideDistance < stride) return;
            strideDistance %= stride;
            GameSound.PlayClip(Pick(footstepClips, null), position, .22f, .94f + (float)variation.NextDouble() * .12f);
        }

        void OnDestroy()
        {
            foreach (var clip in generatedClips) if (clip) Destroy(clip);
            if (Instance == this) Instance = null;
        }

        enum ToneType { Sine, Square, Sawtooth }

        AudioClip CreateToneClip(string clipName, float duration, float freq, float volume, ToneType type)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1, t / .006f) * Mathf.Exp(-t * 65f) * Mathf.Clamp01((duration - t) / .012f);
                float phase = t * freq * Mathf.PI * 2f;
                float val = Mathf.Sin(phase);
                if (type == ToneType.Square) val = val >= 0 ? 0.7f : -0.7f;
                data[i] = val * envelope * volume;
            }

            var clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            generatedClips.Add(clip);
            return clip;
        }

        AudioClip CreateNoiseHitClip(string clipName, float duration, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random(42);
            float filtered = 0;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1, t / .003f) * Mathf.Pow(1f - (float)i / samples, 2.5f);
                filtered = Mathf.Lerp(filtered, (float)rng.NextDouble() * 2f - 1f, .24f);
                float noise = filtered * .7f;
                float lowFreq = Mathf.Sin(i * 115f * Mathf.PI * 2f / sampleRate) * .35f;
                data[i] = (noise + lowFreq) * envelope * volume;
            }

            var clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            generatedClips.Add(clip);
            return clip;
        }

        AudioClip CreateChirpClip(string clipName, float duration, float startFreq, float endFreq, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            float phase = 0, filtered = 0;
            var random = new System.Random(56);
            for (int i = 0; i < samples; i++)
            {
                float frac = (float)i / samples;
                float envelope = Mathf.Sin(frac * Mathf.PI);
                float currentFreq = Mathf.Lerp(startFreq, endFreq, frac);
                phase += currentFreq * Mathf.PI * 2f / sampleRate;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2f - 1f, .07f);
                data[i] = (Mathf.Sin(phase) * .28f + Mathf.Sin(phase * 1.003f) * .12f + filtered * .7f) * envelope * volume;
            }

            var clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            generatedClips.Add(clip);
            return clip;
        }

        AudioClip CreateArpeggioClip(string clipName, float duration, float[] freqs, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / sampleRate;
                float value = 0;
                for (int note = 0; note < freqs.Length; note++)
                {
                    float t = time - note * duration * .55f / freqs.Length;
                    if (t < 0) continue;
                    float envelope = Mathf.Min(1, t / .009f) * Mathf.Exp(-t * 7f);
                    float phase = t * freqs[note] * Mathf.PI * 2f;
                    value += (Mathf.Sin(phase) + .13f * Mathf.Sin(phase * 2.01f)) * envelope;
                }
                data[i] = Mathf.Clamp(value * volume * .42f * Mathf.Clamp01((duration - time) / .05f), -.8f, .8f);
            }

            var clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            generatedClips.Add(clip);
            return clip;
        }

        AudioClip CreateFanfareClip(string clipName, float duration)
        {
            return CreateArpeggioClip(clipName, duration, new[] { 261.63f, 329.63f, 392f, 523.25f, 659.25f }, .5f);
        }

        void PlayUi(AudioClip clip, float volume, int cue, float gap, bool allowMenu = false)
        {
            if (!isActiveAndEnabled || !clip || (!allowMenu && !CanPlayWorld) || Time.unscaledTime < nextCue[cue]) return;
            nextCue[cue] = Time.unscaledTime + gap;
            var voice = uiVoices[nextVoice++ % uiVoices.Length];
            voice.Stop();
            voice.clip = clip;
            voice.pitch = .98f + (float)variation.NextDouble() * .04f;
            voice.volume = volume * EffectsVolume;
            voice.Play();
        }

        public static void PlayClick() { if (Instance) Instance.PlayUi(Instance.Pick(Instance.clickClips, Instance.sfxClick), .24f, 0, .055f, true); }
        public static void PlayBladeHit()
        {
            if (!CanPlayWorld || Time.time < Instance.nextSwing) return;
            Instance.nextSwing = Time.time + .08f;
            var position = Instance.player ? Instance.player.transform.position + Vector3.up : Instance.transform.position;
            GameSound.PlayClip(Instance.Pick(Instance.swingClips, Instance.sfxBladeHit), position, .3f, .94f + (float)Instance.variation.NextDouble() * .12f);
        }
        public static void PlaySpellCast()
        {
            if (!CanPlayWorld || Time.time < Instance.nextSpell) return;
            Instance.nextSpell = Time.time + .12f;
            GameSound.PlayClip(Instance.sfxSpellCast, Instance.player ? Instance.player.transform.position + Vector3.up : Instance.transform.position, .4f);
        }
        public static void PlayLootCoin() { if (Instance) Instance.PlayUi(Instance.Pick(Instance.coinClips, Instance.sfxLootCoin), .38f, 1, .12f); }
        public static void PlayQuestAccept() { if (Instance) Instance.PlayUi(Instance.Pick(Instance.pageClips, Instance.sfxQuestAccept), .4f, 2, .3f); }
        public static void PlayQuestComplete() { if (Instance) Instance.PlayUi(Instance.sfxQuestComplete, .58f, 3, .5f); }
        public static void PlayLevelUp() { if (Instance) Instance.PlayUi(Instance.sfxLevelUp, .6f, 4, 1f); }
        public static void PlayBossVictory() { if (Instance) Instance.PlayUi(Instance.sfxLevelUp, .7f, 4, 1f); }
        public static void PlayZoneDiscover() { if (Instance) Instance.PlayUi(Instance.sfxZoneDiscover, .36f, 5, 1.5f); }
    }
}
