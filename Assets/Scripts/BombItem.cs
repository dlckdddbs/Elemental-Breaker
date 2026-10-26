using System.Collections.Generic;
using UnityEngine;

namespace ElementalBreaker
{
    // 속성 폭탄이야. 누르는 순간 맨 앞 공이 있는 칸 주변에 이번 턴 공 속성으로 데미지를 바로 줘.
    // 그 주변 3x3에 벽돌이 없으면 쓰지 않고 남겨 둬.
    // 폭발 중심에 가까운 가운데 십자(같은 줄, 같은 칸)는 더 세게 맞고, 대각선은 기본 데미지야.
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

        public bool Detonate(int radius, int damageDivisor, int crossMultiplier, float hitStopTime, float hitStopScale)
        {
            Ball front = launcher.FrontBall;
            if (front == null)
                return false;

            // 폭발은 누른 순간 맨 앞 공의 자리에서 터져. 깨진 벽돌 조각과 밀리는 벽돌도 여기서 멀어지는 쪽으로 움직여.
            // 데미지 범위는 그 공이 들어 있는 칸 기준 3x3이고, 맨 위 빈 공간이면 바로 밑 첫째 줄만 맞아.
            Vector2 center = front.transform.position;
            board.WorldToBombCell(center, out int row, out int column);
            board.CollectBricksInRange(row, column, radius, targets);
            // 맞을 벽돌이 없으면 터뜨리지 않아. 폭탄은 아이템 칸에 그대로 남고, 아이템 칸이 취소됐다고 알려 줘.
            if (targets.Count == 0)
                return false;
            ElementType element = launcher.TurnElement;
            int baseDamage = board.BombDamage(damageDivisor);
            // 폭발 위에 띄우는 합계야. 남은 체력보다 큰 데미지는 빼고 실제로 깎인 체력만 더해.
            int total = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                Brick brick = targets[i];
                // 공과 같은 규칙: 유리한 상성이면 2배, 나머지는 1배야. 골드도 공으로 깰 때와 같아.
                int damage = ElementRules.HasAdvantage(element, brick.Element) ? baseDamage * 2 : baseDamage;
                bool cross = brick.Row == row || brick.Column == column;
                if (cross)
                    damage *= crossMultiplier;
                total += Mathf.Min(damage, brick.Health);
                brick.TakeBombHit(damage, element, center, cross);
            }
            targets.Clear();
            // 히트 스톱 동안은 멈춘 장면을 보여 주고, 끝나는 순간 터지게 시간을 맞춰.
            if (effects != null)
                effects.PlayDetonate(center, element, hitStopTime, total);
            if (hitStop != null)
                hitStop.Play(hitStopTime, hitStopScale);
            return true;
        }
    }
}
