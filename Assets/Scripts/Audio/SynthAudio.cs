using UnityEngine;

namespace SokoBan.Audio
{
    public enum WaveType { Sine, Triangle, Square, Sawtooth }

    // 运行时合成音效（对照 audio.js 的 Web Audio 振荡器，无外部资源）。
    public class SynthAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _source;
        private bool _enabled = true;

        private AudioClip _click, _move, _push, _grab, _release, _fill, _undo, _error, _win;

        private void Awake()
        {
            if (FindObjectOfType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        public bool IsEnabled() => _enabled;
        public void SetEnabled(bool v) => _enabled = v;

        public void Click() => Play(ref _click, 660f, 0.06f, WaveType.Triangle, 0.10f, 0f);
        public void Move() => Play(ref _move, 320f, 0.05f, WaveType.Sine, 0.08f, 250f);
        public void Push() => Play(ref _push, 150f, 0.09f, WaveType.Square, 0.07f, 110f);
        public void Grab() => Play(ref _grab, 220f, 0.16f, WaveType.Sine, 0.10f, 560f);
        public void Release() => Play(ref _release, 560f, 0.10f, WaveType.Sine, 0.08f, 220f);
        public void Fill() => Play(ref _fill, 150f, 0.15f, WaveType.Square, 0.07f, 80f);
        public void Undo() => Play(ref _undo, 440f, 0.05f, WaveType.Triangle, 0.08f, 360f);
        public void Error() => Play(ref _error, 170f, 0.12f, WaveType.Sawtooth, 0.05f, 120f);
        public void Win()
        {
            if (_win == null) _win = MakeWin();
            PlayClip(_win);
        }

        private void Play(ref AudioClip clip, float freq, float dur, WaveType wave, float gain, float slideTo)
        {
            if (clip == null) clip = MakeTone(freq, dur, wave, gain, slideTo);
            PlayClip(clip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (_enabled && clip != null && _source != null) _source.PlayOneShot(clip);
        }

        private static AudioClip MakeTone(float freq, float dur, WaveType wave, float gain, float slideTo)
        {
            var data = new float[(int)((dur + 0.03f) * SampleRate)];
            Render(data, 0, freq, dur, wave, gain, slideTo);
            var clip = AudioClip.Create("tone", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeWin()
        {
            float[] freqs = { 523f, 659f, 784f, 1046f };
            var data = new float[(int)((0.09f * 4f + 0.20f + 0.03f) * SampleRate)];
            for (int i = 0; i < freqs.Length; i++)
                Render(data, (int)(i * 0.09f * SampleRate), freqs[i], 0.20f, WaveType.Triangle, 0.14f, 0f);
            var clip = AudioClip.Create("win", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 把一段 tone 叠加进 data（频率指数滑动 + 指数包络，对照 audio.js 的 tone）。
        private static void Render(float[] data, int start, float freq, float dur, WaveType wave, float gain, float slideTo)
        {
            int n = (int)((dur + 0.03f) * SampleRate);
            float attack = 0.012f;
            float twoPi = Mathf.PI * 2f;
            float phase = 0f;

            for (int i = 0; i < n; i++)
            {
                int idx = start + i;
                if (idx >= data.Length) break;
                float t = i / (float)SampleRate;

                float f = freq;
                if (slideTo > 0f && dur > 0f && t < dur)
                {
                    float k = t / dur;
                    f = freq * Mathf.Pow(slideTo / freq, k);
                }
                phase += twoPi * f / SampleRate;

                float g;
                if (t < attack)
                    g = 0.0001f * Mathf.Pow(gain / 0.0001f, t / attack);
                else
                    g = gain * Mathf.Pow(0.0001f / gain, (t - attack) / Mathf.Max(0.0001f, dur - attack));
                g = Mathf.Clamp(g, 0f, 1f);

                data[idx] += Wave(wave, phase) * g;
            }
        }

        private static float Wave(WaveType wave, float phase)
        {
            switch (wave)
            {
                case WaveType.Triangle: return 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(phase));
                case WaveType.Square: return Mathf.Sin(phase) >= 0f ? 1f : -1f;
                case WaveType.Sawtooth: return 2f * (phase / (2f * Mathf.PI) - Mathf.Floor(0.5f + phase / (2f * Mathf.PI)));
                default: return Mathf.Sin(phase);
            }
        }
    }
}
