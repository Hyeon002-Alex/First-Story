using System;

// 붕괴/균열 시스템. 상태 없음. 게이지는 EnemyUnit, 받는피해증가는 RuntimeStats
// 일반/정예 = 붕괴, 보스 = 균열. 임계만 다르고 게이지는 단일 공용
// 발생 효과: 받는 피해 증가 1.50 공통 / 행동최소는 붕괴만
public static class BreakCrackSystem
{
    private static readonly float _brokenMod = 1.50f;   // 발생 시 받는피해증가 배율

    // 순수 조회. 대상의 받는피해증가배율
    public static float GetDamageMod(BattleUnit target)
    { 
        if(target == null)
            throw new ArgumentNullException(nameof(target));

        return target.BreakDamageMod;
    }

    // 게이지 누적. 붕괴량 직독
    // 붕괴량 0 허용
    public static void Accumulate(EnemyUnit enemy, int amount)
    { 
        if(enemy == null)
            throw new ArgumentNullException(nameof(enemy));
        if (amount < 0)
            return;
        if (IsBroken(enemy))
            return;

        enemy.SetGauge(enemy.CurrBreakOrCrackGauge + amount);
    }

    // 임계 판정 -> 발생. 발생하면 true
    // 발생: 게이지 0 초기화 + 받는피해증가 1.50, 다음 턴 종료까지
    // + 붕괴이고 미행동이면 intent 무효 표시
    public static bool CheckAndTrigger(EnemyUnit enemy, int currentTurn, IntentSystem intentSystem)
    { 
        if (enemy == null)
            throw new ArgumentNullException(nameof(enemy));
        if(intentSystem == null)
            throw new ArgumentNullException(nameof(intentSystem));
        if (IsBroken(enemy))
            return false;   // 이미 붕괴/균열이면 재발생 없음

        int threshold = enemy.IsBoss ? enemy.MaxCrackGauge : enemy.MaxBreakGauge;
        if (enemy.CurrBreakOrCrackGauge < threshold)
            return false;

        enemy.SetGauge(0);
        enemy.SetBreakDamageMod(_brokenMod, currentTurn + 1);

        // 붕괴만 행동 취소. 이미 행동했으면 취소 안 함
        if (!enemy.IsBoss && !enemy.ActedThisTurn)
            intentSystem.Cancel(enemy);

        return true;
    }

    // 8단계 만료. 다음 턴 도달 시 배율 1.00 복귀
    public static void ExpireDamageMod(BattleUnit unit, int currentTurn)
    { 
        if(unit == null)
            throw new ArgumentNullException(nameof(unit));
        if (IsBroken(unit) && unit.BreakDamageModExpireTurn <= currentTurn)
            unit.SetBreakDamageMod(1.00f, 0);
    }

    // 붕괴/균열 상태 = 받는피해증가 활성
    private static bool IsBroken(BattleUnit unit) => unit.BreakDamageMod > 1.00f;
}