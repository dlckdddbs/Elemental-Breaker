using System;
using UnityEngine;

namespace ElementalBreaker
{
    public enum SoundCue
    {
        Launch, HitNormal, HitFire, HitWater, HitGrass, Break, Retire, Pickup,
        BombNormal, BombFire, BombWater, BombGrass, BoardChange,
        Click, Select, ShopOpen, ShopClose, Purchase, Denied, GameOver,
        // 얼음 아이템: 얼어붙을 때, 턴 끝에 풀릴 때. 소리 파일은 아직 비어 있어.
        IceFreeze, IceRelease,
        // 폭탄 데미지 합계 숫자가 다 올라가서 톡 커질 때. 소리 파일은 아직 비어 있어.
        BombTotal,
        // 턴 이벤트: 보상 배너가 나타날 때, 해금 아이템이 아이템 칸에 닿을 때, 동전이 골드에 닿을 때, 경고 배너가 나타날 때.
        RewardBanner, ItemArrive, CoinArrive, Warning,
        // 게임 오버: 벽돌이 기준선에 닿는 순간의 짧고 둔탁한 "쿵". 직접 만든 소리야.
        GameOverThud
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
