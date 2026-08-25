using System;
using System.Collections.Generic;

// 한 적 intent 읽기 전용 투영. 정보확인 게이트로 공개 수준을 나눔
// 기본공개(대상/방향)은 항상, 확인 후 공개(행동명/부가효과/회피불가)는 IsRevealed일 때만 성립
// 게이트를 이 타입 생성 단계에서 강제: 미확인 view엔 확인 후 정보가 물리적으로 없음. 기본값
// -> 상위 UI 버그로도 미공개 정보를 노출할 수 없음. 정보전 정체성을 데이터 계층에서 보장
public sealed class IntentView
{ 
    // === 기본 공개: 항상 유효 === //
    public BattleUnit Target { get; }           // 예정 대상. null 허용(대상 상실/범위 대표 없음)
    public AttackDirection Direction { get; }
    public bool IsRevealed { get; }             // 확인 후 공개 필드으 유효성 신호

    // === 확인 후 공개: IsRevealed일 때만 실제값, 아니면 기본값 === //
    public string DisplayName { get; }                      // 미확인 시 null
    public IReadOnlyList<StatusEffectData> Effects { get; } // 미확인 시 빈 목록
    public bool IsUnavoidable { get; }                      // 미확인 시 false
    // 캐릭터 스킬 설계 후 수렴. 면역 등 캐릭터별 공개 범위 차이는 확정 후 여기 확장

    // 팩토리 전용 생성자. Basic/Full 두 통로로만 만들어
    // "미확인데 확인정보 보유" 같은 모순 상태를 구조적으로 차단
    private IntentView(
        BattleUnit target, AttackDirection direction, bool isRevealed,
        string displayName, IReadOnlyList<StatusEffectData> effects, bool isUnavoidable)
    { 
        Target = target;
        Direction = direction;
        IsRevealed = isRevealed;
        DisplayName = displayName;
        Effects = effects;
        IsUnavoidable = isUnavoidable;
    }

    // 미확인 view: 확인 후 정보를 싣지 않음. 대상/방향만 노출
    public static IntentView Basic(BattleUnit target, AttackDirection direction)
        => new IntentView(target, direction, false, null, Array.Empty<StatusEffectData>(), false);

    // 확인 view: Skill 파생 확인정보를 함께 실음. effects는 SkillData.Effects 그대로 전달
    public static IntentView Full(
        BattleUnit target, AttackDirection direction,
        string displayName, IReadOnlyList<StatusEffectData> effects, bool isUnavoidable)
        => new IntentView(target, direction, true, displayName, effects, isUnavoidable);
}