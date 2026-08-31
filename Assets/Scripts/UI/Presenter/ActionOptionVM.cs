using System;
using System.Collections.Generic;

// 행동 버튼 1개. View는 이 필드만으로 그림: ActionChoice/SkillData를 직접 안 만짐. 얇은 view 원칙
public sealed class ActionOptionVM
{
    public ActionKind Kind { get; }
    public string Label { get; }
    public int ApCost { get; }
    public bool RequiresTarget { get; }   // false면 SelectAction 한 번으로 즉시 확정(Defense/EndTurn)

    // Defense 전용. 이 방향을 고르면 맞을 예상피해(공격자별). 그 외 Kind는 항상 빈 목록
    // "대상별 예상피해"가 아니라 "행동 자체의 예상피해"로 버튼에 직접 붙음
    public IReadOnlyList<IncomingPreviewEntryVM> IncomingPreview { get; }

    // 조립용 내부 참조. View는 위 필드로 표시만 하고, 명령 조립(Kind별 ActionCommand 팩토리 매핑)은
    // AllyInputPresenter.SelectAction 내부가 전담: View가 Source를 직접 해석하면 로직이 새는 것
    public ActionChoice Source { get; }

    public ActionOptionVM(
        ActionKind kind, string label, int apCost, bool requiresTarget,
        IReadOnlyList<IncomingPreviewEntryVM> incomingPreview, ActionChoice source)
    {
        Kind = kind;
        Label = label;
        ApCost = apCost;
        RequiresTarget = requiresTarget;
        IncomingPreview = incomingPreview ?? Array.Empty<IncomingPreviewEntryVM>();
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }
}