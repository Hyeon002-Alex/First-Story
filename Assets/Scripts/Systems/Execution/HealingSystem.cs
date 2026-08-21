using System;

// 확정 회복량을 HP에 적용. ModifyHP 통로의 [0, maxHP]의 클램프가 초과 회복 자동 절삭
// 방어/방향/붕괴 무보정
public static class HealingSystem
{
    public static void Apply(BattleUnit target, int amount)
    { 
        if(target == null)
            throw new ArgumentNullException(nameof(target));
        if(amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "회복량은 음수 불가");

        target.ModifyHP(amount);
    }
}