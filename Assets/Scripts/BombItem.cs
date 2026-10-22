using System.Collections.Generic;
using UnityEngine;

namespace ElementalBreaker
{
    // 속성 폭탄이야. 누르는 순간 맨 앞 공이 있는 칸 주변에 이번 턴 공 속성으로 데미지를 바로 줘.
    // 데미지를 연출보다 먼저 넣어서, 뒤따라오는 공이 곧 깨질 벽돌을 먼저 치는 손해가 없게 해.
    public sealed class BombItem
    {
        private readonly BrickBoard board;
        private readonly BallLauncher launcher;
        private readonly HitStop hitStop;
        private readonly BombEffects effects;
        // 범위 안 벽돌을 담을 목록이야. 매번 새로 만들지 않고 다시 써.
        private readonly List<Brick> targets = new List<Brick>(9);

        public BombItem(BrickBoard board, BallLauncher launcher, HitStop hitStop, BombEffects effects)
        {
            this.board = board;
            this.launcher = launcher;
            this.hitStop = hitStop;
            this.effects = effects;
        }

        public bool Detonate(int radius, int damageDivisor, float hitStopTime, float hitStopScale)
        {
            Ball front = launcher.FrontBall;
            if (front == null)
                return false;

            board.WorldToCell(front.transform.position, out int row, out int column);
            board.GetRangeBounds(row, column, radius, out Vector2 min, out Vector2 max);
            // 폭발은 범위 한가운데에서 터져. 깨진 벽돌 조각도 여기서 멀어지는 쪽으로 튀어.
            Vector2 center = (min + max) * 0.5f;
            board.CollectBricksInRange(row, column, radius, targets);
            ElementType element = launcher.TurnElement;
            int baseDamage = board.BombDamage(damageDivisor);
            for (int i = 0; i < targets.Count; i++)
            {
                Brick brick = targets[i];
                // 공과 같은 규칙: 유리한 상성이면 2배, 나머지는 1배야. 골드도 공으로 깰 때와 같아.
                int damage = ElementRules.HasAdvantage(element, brick.Element) ? baseDamage * 2 : baseDamage;
                brick.TakeBombHit(damage, element, center);
            }
            targets.Clear();
            // 히트 스톱 동안은 멈춘 장면을 보여 주고, 끝나는 순간 터지게 시간을 맞춰.
            if (effects != null)
                effects.PlayDetonate(min, max, element, hitStopTime);
            if (hitStop != null)
                hitStop.Play(hitStopTime, hitStopScale);
            return true;
        }
    }
}
