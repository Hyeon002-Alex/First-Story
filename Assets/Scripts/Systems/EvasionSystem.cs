using System;

// 회피 시스템. 상태 없음. 카운트는 유닛에, 규칙만 여기
// 상한3 클램프는 이 시스템 소유. 유닛 통로 SetEvasion은 음수만 방지
public static class EvasionSystem
{
    private static readonly int _maxEvasion = 3;    // 회피 상한

    // 순수 조회. 회피 보유 여부
    public static bool HasEvasion(BattleUnit unit)
    { 
        if(unit == null)
            throw new ArgumentNullException(nameof(unit));

        return unit.EvasionCount > 0;
    }

    // 회피 부여. 가산 + 상한3 클램프. 재부여도 같은 경로
    public static void Grant(BattleUnit unit, int count)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));
        if(count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "부여량은 음수 불가");

        unit.SetEvasion(Math.Min(unit.EvasionCount + count, _maxEvasion));
    }

    // 회피 1회 즉시 소모. 판정 시점 확정
    public static void Consume(BattleUnit unit)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        unit.SetEvasion(unit.EvasionCount - 1);
    }
}