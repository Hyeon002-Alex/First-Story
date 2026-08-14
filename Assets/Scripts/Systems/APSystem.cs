using System;
using System.Collections.Generic;

// AP 시스템. 상태 없음, 아군 유닛에 규칙만 적용
// 값은 AllyUnit._currAP에, 계산, 판단은 이 시스템에
public static class APSystem
{
    private static readonly int _apGainPerTurn = 2;   // 턴당 회복량
    private static readonly int _maxAP = 6;           // AP 상한

    // 턴 시작 AP 회복
    // 전투 불능 아군 스킵. 생존 판정을 이 시스템 한 곳에 둠
    public static void RecoverAll(IReadOnlyList<AllyUnit> allies)
    {
        foreach (AllyUnit ally in allies)
        {
            if (ally.IsIncapacitated)
                continue;

            // AP회복보정: 부호 델타 합산. 다음 턴부터 지연반영
            // 여러 개 겹여 음수가 되면 0 클램프
            int gain = _apGainPerTurn + (int)ally.SumStatusMag(EffectKind.APRecoveryMod);
            if (gain < 0)
                gain = 0;

            int recovered = Math.Min(ally.CurrAP + gain, _maxAP);
            ally.SetAP(recovered);
        }
    }

    // AP 충분 여부만 답함. 실제 차단은 호출자, 행동 선택 쪽
    public static bool CanAfford(AllyUnit ally, int cost) => ally.CurrAP >= cost;

    // 행동 확정 시점 1회 소모. SetAP의 0 하한이 음수 방지
    // 음수 코스트 차단. 음수면 회복 방향이라 상한6 우회 경로가 됨
    public static void Consume(AllyUnit ally, int cost)
    {
        if (cost < 0)
            throw new ArgumentOutOfRangeException(nameof(cost), "소모 비용은 음수 불가");

        ally.SetAP(ally.CurrAP - cost);
    }
}