using System.Collections.Generic;

// 대상 결정 결과. 최종 대상 목록 + 대상상실 여부
// 대상상실 = 단일 대상이 이미 전투불능인 경우. 행동 취소, 재타겟 없음
public readonly struct TargetingResult
{ 
    public IReadOnlyList<BattleUnit> Targets { get; }
    public bool TargetLost { get; }

    public TargetingResult(IReadOnlyList<BattleUnit> targets, bool targetLost)
    { 
        Targets = targets;
        TargetLost = targetLost;
    }
}