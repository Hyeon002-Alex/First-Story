using System;
using System.Collections.Generic;

// 보호막 부여. 기존 보호막에 가산. 중첩 = 가산
// 흡수는 DamageSystem 소유. HP를 Damage/Healing 둘로 나누듯, 보호막도 소모와 부여를 나눔
// 만료는 외부에서 소유
public static class ShieldSystem
{
    public static void Grant(BattleUnit target, int amount)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if(amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "보호막은 음수 불가");

        target.SetShield(target.Shield + amount);   // 가산 중첩
    }

    // 웨이브 전환 강제소거: 남은 보호막 무관 전원 0. 턴종료 만료 통로와 별개. 웨이브 전용
    public static void ClearAll(IEnumerable<BattleUnit> units)
    { 
        if(units == null)
            throw new ArgumentNullException(nameof(units));

        foreach (BattleUnit u in units)
        {
            u.SetShield(0);
        }
    }
}