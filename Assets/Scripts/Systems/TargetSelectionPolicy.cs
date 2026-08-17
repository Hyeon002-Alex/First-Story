using System;
using System.Collections.Generic;

// 단일 대상 선택 해석. 순수 조회. 대상 후보 중 우선순위 1명 산출
// 동률 시 목록 슬롯 인덱스 오름차순 고정. 동점은 항상 먼저 나온 유닛. 무작위 없음
public static class TargetSelectionPolicy
{
    // candidates = 생존 대상 후보, 플레이어 측. 파티슬롯 순서
    // 비어있으면 null: 대상 상실 -> 실행 시 불발
    public static AllyUnit Select(TargetPolicy policy, IReadOnlyList<AllyUnit> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        switch (policy)
        {
            case TargetPolicy.FirstAlive:
                return candidates[0];   // 슬롯 앞 첫 생존자
            case TargetPolicy.LowestHP:
                return PickBy(candidates, (best, c) => c.CurrHP < best.CurrHP);
            case TargetPolicy.HighestHP:
                return PickBy(candidates, (best, c) => c.CurrHP > best.CurrHP);
            case TargetPolicy.HighestAttack:
                return PickBy(candidates, (best, c) => c.EffectiveAttack > best.EffectiveAttack);
            default:
                return candidates[0];   // 미정의 정책 = FirstAlive 폴백
        }
    }

    // 첫 요소 기준 순회, isBetter가 참일 때만 교체
    private static AllyUnit PickBy(IReadOnlyList<AllyUnit> candidates, Func<AllyUnit, AllyUnit, bool> isBetter)
    {
        AllyUnit best = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            if (isBetter(best, candidates[i]))
                best = candidates[i];
        }
        return best;
    }
}