using System;
using System.Collections.Generic;

// 적 의도 표시 1건. IntentView를 View 친화적 형태로 옮긴 것
// 정보확인 게이트는 IntentView 생성 단계에서 이미 강제ㅚㅁ
// -> 미확인 view에는 확인 후 정보가 없어서, 여긴 그 결과를 그대로 문자열로 바꾸기만 함
public sealed class IntentDisplayVM
{
    public BattleUnit Target { get; }
    public string TargetLabel { get; }          // Target null이면 null
    public AttackDirection Direction { get; }
    public bool IsRevealed { get; }
    public string DisplayName { get; }          // 미확인 시 null. IntentView 그대로
    public IReadOnlyList<string> EffectLabels { get; }  // 미확인 시 빈 목록
    public bool IsUnavoidable { get; }                  // 미확인 시 false

    public IntentDisplayVM(
        BattleUnit target, string targetLabel, AttackDirection direction, bool isRevealed, 
        string displayName, IReadOnlyList<string> effectLabels, bool isUnavoidable)
    {
        Target = target;
        TargetLabel = targetLabel;
        Direction = direction;
        IsRevealed = isRevealed;
        DisplayName = displayName;
        EffectLabels = effectLabels ?? Array.Empty<string>();
        IsUnavoidable = isUnavoidable;
    }
}