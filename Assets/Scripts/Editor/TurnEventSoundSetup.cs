using System;
using UnityEditor;
using UnityEngine;

namespace ElementalBreaker.Editor
{
    // 턴 이벤트, 얼음, 게임 오버 "쿵" 소리를 소리 목록(GameAudioCatalog)의 빈칸에 넣어. 볼륨도 같이 맞춰.
    // 이미 소리가 들어 있는 칸은 건드리지 않아서, 인스펙터에서 직접 바꾼 소리는 그대로 남아.
    // 스크립트를 다시 불러올 때와 이 소리 파일들을 가져올 때 확인해.
    public sealed class TurnEventSoundSetup : AssetPostprocessor
    {
        private const string CatalogPath = "Assets/Resources/GameAudioCatalog.asset";
        private const string Folder = "Assets/Audio/SFX/";

        // Kenney 소리는 CC0이고 출처는 Assets/Audio/Licenses/Kenney/SoundSources.csv에,
        // 직접 만든 소리는 Assets/Audio/Licenses/Generated/GeneratedSounds.csv에 적어 뒀어.
        private static readonly (SoundCue cue, string path, float volume)[] Sounds =
        {
            (SoundCue.IceFreeze, "Kenney/Ice/ice_freeze.ogg", 0.5f),
            (SoundCue.IceRelease, "Kenney/Ice/ice_release.ogg", 0.5f),
            (SoundCue.RewardBanner, "Kenney/Reward/reward_banner.ogg", 0.5f),
            (SoundCue.ItemArrive, "Kenney/UI/item_arrive.ogg", 0.55f),
            // 원본이 꽤 커서(최대 0dB) 조금 줄여.
            (SoundCue.CoinArrive, "Kenney/Reward/coin_arrive.ogg", 0.45f),
            (SoundCue.Warning, "Kenney/Warning/debuff_warning.ogg", 0.45f),
            (SoundCue.GameOverThud, "Generated/game_over_thud.wav", 0.7f)
        };

        [InitializeOnLoadMethod]
        private static void OnLoad() => EditorApplication.delayCall += Assign;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.StartsWith(Folder))
                    continue;
                EditorApplication.delayCall += Assign;
                return;
            }
        }

        private static void Assign()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            GameAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(CatalogPath);
            if (catalog == null)
                return;
            bool changed = false;
            // 소리 종류가 늘었으면 목록 칸 수도 늘려. 원래 있던 칸은 그대로 옮겨.
            int count = Enum.GetValues(typeof(SoundCue)).Length;
            if (catalog.sounds == null || catalog.sounds.Length < count)
            {
                GameAudioCatalog.Entry[] sounds = new GameAudioCatalog.Entry[count];
                if (catalog.sounds != null)
                    Array.Copy(catalog.sounds, sounds, catalog.sounds.Length);
                catalog.sounds = sounds;
                changed = true;
            }
            foreach ((SoundCue cue, string path, float volume) in Sounds)
            {
                int index = (int)cue;
                if (catalog.sounds[index].clip != null)
                    continue;
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + path);
                if (clip == null)
                    continue;
                catalog.sounds[index].clip = clip;
                catalog.sounds[index].volume = volume;
                changed = true;
            }
            if (!changed)
                return;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("소리 목록에 턴 이벤트와 얼음 소리를 넣었어. 바꾸고 싶으면 Resources/GameAudioCatalog에서 바꿔.");
        }
    }
}
