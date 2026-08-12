using System;

// 확정 최종 피해를 유닛에 적용. 보호막 먼저 흡수 -> 남은 만큼 Hp 감소
// 계산은 CombatCalculator 소유. 여긴 적용만
public static class DamageSystem
{
    // finalDamage = 방향/방어/붕괴 다 반영 끝난 최종값
    // 반환 = 흡수/HP손실 분해
    public static DamageApplication Apply(BattleUnit target, int finalDamage)
    { 
        if(target == null)
            throw new ArgumentNullException(nameof(target));
        if(finalDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(finalDamage), "최종 피해는 음수 불가");

        int shield = target.Shield;
        int absorbed = Math.Min(shield, finalDamage);   // 보호막이 흡수하는 몫
        int hpLost = finalDamage - absorbed;            // HP로 넘어가는 몫

        target.SetShield(shield - absorbed);    // 보호막 소모
        target.ModifyHP(-hpLost);               // HP감소. RutimeStats.ModifyHP에서 [0, maxHP] 클램프

        return new DamageApplication(absorbed, hpLost);
    }
}