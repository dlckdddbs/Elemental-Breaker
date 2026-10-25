using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementalBreaker
{
    // 고정 개수의 재생기를 돌려 써. 씬에는 저장하지 않아.
    public sealed class GameAudio : MonoBehaviour
    {
        private static GameAudio instance;
        private GameAudioCatalog catalog;
        private readonly AudioSource[] voices = new AudioSource[12];
        // 소리 종류마다 다음에 다시 낼 수 있는 시각이야. 종류가 늘어도 칸 수가 맞게 SoundCue 개수로 잡아.
        private readonly float[] nextAllowed = new float[System.Enum.GetValues(typeof(SoundCue)).Length];
        private float nextImpact;
        private int importantCursor = 8;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (instance != null)
                return;
            instance = new GameObject("Game Audio (Runtime)").AddComponent<GameAudio>();
            DontDestroyOnLoad(instance.gameObject);
            instance.catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            if (instance.catalog == null)
                Debug.LogError("GameAudioCatalog 사운드 설정이 없어.");
            for (int i = 0; i < instance.voices.Length; i++)
            {
                AudioSource source = instance.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.priority = i < 8 ? 160 : 64;
                instance.voices[i] = source;
            }
            SceneManager.sceneLoaded += instance.OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 재시작하면 이전 판의 소리와 제한 시간을 비워.
            for (int i = 0; i < voices.Length; i++)
                voices[i].Stop();
            for (int i = 0; i < nextAllowed.Length; i++)
                nextAllowed[i] = 0f;
            nextImpact = 0f;
        }

        // 속성과 관계없이 같은 물 피격음을 써.
        public static void Hit(ElementType element) => Play(SoundCue.HitWater);
        public static void Bomb(ElementType element) => Play((SoundCue)((int)SoundCue.BombNormal + (int)element));

        public static void Play(SoundCue cue)
        {
            if (!Application.isPlaying || instance == null || instance.catalog == null)
                return;
            instance.PlayInternal(cue);
        }

        private void PlayInternal(SoundCue cue)
        {
            int index = (int)cue;
            if (catalog.sounds == null || index < 0 || index >= catalog.sounds.Length)
                return;
            GameAudioCatalog.Entry entry = catalog.sounds[index];
            if (entry.clip == null)
                return;
            float now = Time.unscaledTime;
            bool impact = cue >= SoundCue.HitNormal && cue <= SoundCue.Break;
            if (now < nextAllowed[index] || (impact && now < nextImpact))
                return;
            // 퇴장음이 끝나기 전에는 다른 공의 퇴장음을 겹치지 않아.
            if (cue == SoundCue.Retire)
                for (int i = 0; i < 8; i++)
                    if (voices[i].isPlaying && voices[i].clip == entry.clip)
                        return;
            // 피격이 몰려도 초당 최대 20번만 들려 줘.
            if (impact)
                nextImpact = now + 0.05f;
            nextAllowed[index] = now + (cue == SoundCue.Retire ? 0.2f : cue == SoundCue.Launch ? 0.08f : 0.05f);
            bool important = cue >= SoundCue.BombNormal || cue == SoundCue.Pickup;
            int first = important ? 8 : 0;
            int last = important ? voices.Length : 8;
            AudioSource voice = null;
            for (int i = first; i < last; i++)
                if (!voices[i].isPlaying) { voice = voices[i]; break; }
            // 폭탄과 UI용 자리는 공 소리가 차지하지 않아.
            if (voice == null && important)
            {
                voice = voices[importantCursor];
                importantCursor = 8 + (importantCursor - 8 + 1) % 4;
            }
            if (voice == null)
                return;
            voice.Stop();
            voice.clip = entry.clip;
            voice.volume = entry.volume;
            voice.Play();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this)
                instance = null;
        }
    }
}
