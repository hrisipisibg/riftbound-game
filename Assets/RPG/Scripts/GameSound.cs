using UnityEngine;

namespace Riftbound
{
    public enum SoundCue { Hit, Heal, Rune, Guard }
    public static class GameSound
    {
        static readonly AudioClip[] Clips = new AudioClip[4];
        static readonly AudioSource[] Voices = new AudioSource[8];
        static readonly float[] NextCue = new float[4];
        static readonly System.Random Variation = new System.Random(4508);
        static AudioClip[] hits, guards;
        static int nextVoice, soundFrame = -1, soundsThisFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlayback()
        {
            for (int i = 0; i < NextCue.Length; i++) NextCue[i] = 0;
            nextVoice = 0;
            soundFrame = -1;
        }

        public static void Play(SoundCue cue, Vector3 position)
        {
            if (!Application.isPlaying || !AudioEffects.CanPlayWorld) return;
            if (cue == SoundCue.Hit && AudioEffects.Instance)
            {
                var player = AudioEffects.Instance.GetComponent<PlayerController>();
                if (player && player.Blocking && (position - player.transform.position).sqrMagnitude < .1f) cue = SoundCue.Guard;
            }
            int index = (int)cue;
            if (index < 0 || index >= Clips.Length || Time.time < NextCue[index]) return;
            NextCue[index] = Time.time + (cue == SoundCue.Hit ? .045f : .12f);
            if (hits == null)
            {
                hits = new[] { AudioEffects.LoadRecording("impactPunch_medium_000"), AudioEffects.LoadRecording("impactPunch_medium_001"), AudioEffects.LoadRecording("impactPunch_medium_002") };
                guards = new[] { AudioEffects.LoadRecording("impactMetal_light_000"), AudioEffects.LoadRecording("impactMetal_light_001") };
            }
            var recording = cue == SoundCue.Hit ? hits[Variation.Next(hits.Length)] : cue == SoundCue.Guard ? guards[Variation.Next(guards.Length)] : null;
            if (recording)
            {
                PlayClip(recording, position, cue == SoundCue.Hit ? .44f : .36f, .94f + (float)Variation.NextDouble() * .12f);
                return;
            }
            if (!Clips[index])
            {
                const int rate = 22050; float duration = cue == SoundCue.Rune ? .8f : cue == SoundCue.Heal ? .58f : .18f;
                var samples = new float[Mathf.CeilToInt(rate * duration)]; var random = new System.Random(81 + index);
                float filtered = 0, phase = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = i / (float)rate;
                    float envelope = Mathf.Min(1, t / .012f) * Mathf.Exp(-t * (cue == SoundCue.Hit ? 20 : 5)) * Mathf.Clamp01((duration - t) / .06f);
                    filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, .1f);
                    float frequency = cue == SoundCue.Heal ? 520 + t * 100 : cue == SoundCue.Rune ? 392 : 155;
                    phase += 2 * Mathf.PI * frequency / rate;
                    float tone = Mathf.Sin(phase) * .3f + Mathf.Sin(phase * 1.5f) * .12f + Mathf.Sin(phase * 2.003f) * .045f;
                    samples[i] = envelope * (cue == SoundCue.Hit ? filtered * .65f + tone * .35f : tone + filtered * .16f) * .48f;
                }
                Clips[index] = AudioClip.Create("Riftbound " + cue, samples.Length, 1, rate, false); Clips[index].SetData(samples, 0);
            }
            PlayClip(Clips[index], position, .46f);
        }

        public static void StopWorldVoices()
        {
            foreach (var voice in Voices) if (voice && voice.isPlaying) voice.Stop();
        }

        public static void PlayClip(AudioClip clip, Vector3 position, float volume, float pitch = 1f)
        {
            if (!clip || !AudioEffects.CanPlayWorld) return;
            if (soundFrame != Time.frameCount) { soundFrame = Time.frameCount; soundsThisFrame = 0; }
            if (soundsThisFrame++ >= 4) return;
            int selected = nextVoice++ % Voices.Length;
            for (int i = 0; i < Voices.Length; i++)
                if (!Voices[i] || !Voices[i].isPlaying) { selected = i; break; }
            if (!Voices[selected])
            {
                var go = new GameObject("World sound voice " + selected);
                var voice = go.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 2;
                voice.maxDistance = 34;
                voice.dopplerLevel = 0;
                voice.priority = 140;
                Voices[selected] = voice;
            }
            var source = Voices[selected];
            source.Stop();
            source.transform.position = position;
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume) * AudioEffects.EffectsVolume;
            source.Play();
        }
    }
}
