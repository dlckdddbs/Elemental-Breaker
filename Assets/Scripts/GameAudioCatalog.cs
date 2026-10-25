using System;
using UnityEngine;

namespace ElementalBreaker
{
    public enum SoundCue
    {
        Launch, HitNormal, HitFire, HitWater, HitGrass, Break, Retire, Pickup,
        BombNormal, BombFire, BombWater, BombGrass, BoardChange,
        Click, Select, ShopOpen, ShopClose, Purchase, Denied, GameOver,
        // 얼음 아이템: 얼어붙을 때, 턴 끝에 풀릴 때. 카탈로그 마지막 두 칸이고 소리 파일은 아직 비어 있어.
        IceFreeze, IceRelease
    }

    // 소리 연결과 볼륨을 한곳에 모아.
    public sealed class GameAudioCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }
        public Entry[] sounds;
    }
}
