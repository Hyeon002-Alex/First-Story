using System;
using System.Collections.Generic;
using System.Linq;

// AllyChoices를 받아 대상선택 상태머신을 굴리고 클릭은 ActionCommand로 조립
// Presenter는 BattleSnapshot/IntentSystem/ChoiceQuerySystem을 모름
// ChoiceQuerySystem.GetChoices 호출은 드라이버의 몫. 결과만 여기로 들어옴
public sealed class AllyInputPresenter
{
    public enum PresenterState
    {
        AwaitingAction,
        AwaitingTarget,
        Committed
    }

    public AllyUnit Ally { get; }
    public PresenterState State { get; private set; }
    public IReadOnlyList<ActionOptionVM> ActionOptions { get; }

    // AwaitingTArget일 때만 실제 후보로 채워짐. 그 외엔 빈 목록
    public IReadOnlyList<TargetOptionVM> TargetOptions { get; private set; } = Array.Empty<TargetOptionVM>();

    public bool HasResult => State == PresenterState.Committed;
    public ActionCommand Result { get; private set; }

    private ActionChoice _pendingChoice;    // AwatingTarget 동안 보류 중인 행동. Back()에서 버림

    public AllyInputPresenter(AllyChoices choices)
    {
        if (choices == null) 
            throw new ArgumentNullException(nameof(choices));

        Ally = choices.Ally;
        ActionOptions = choices.Choices.Select(ToActionOptionVM).ToList();
        State = PresenterState.AwaitingAction;
    }

    // 행동 버튼 클릭. 대상이 필요 없는 행동은 여기서 바로 확정
    public void SelectAction(ActionOptionVM option)
    {
        if (option == null)
            throw new ArgumentNullException(nameof(option));
        if (State != PresenterState.AwaitingAction)
            throw new InvalidOperationException($"행동 선택은 AwaitingAction 상태에서만 가능(현재 {State})");
        if (!ActionOptions.Contains(option))
            throw new ArgumentException("현재 선택지에 없는 옵션", nameof(option));

        if (!option.RequiresTarget)
        {
            Commit(Build(option.Source, null));
            return;
        }

        _pendingChoice = option.Source;
        TargetOptions = option.Source.ValidTargets.Select(u => ToTargetOptionVM(option.Source, u)).ToList();
        State = PresenterState.AwaitingTarget;
    }

    // 대상 클릭. 확정
    public void SelectTarget(TargetOptionVM target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (State != PresenterState.AwaitingTarget)
            throw new InvalidOperationException($"대상 선택은 AwaitingTarget 상태에서만 가능(현재 {State})");
        if (!TargetOptions.Contains(target))
            throw new ArgumentException("현재 대상 후보에 없는 대상", nameof(target));

        Commit(Build(_pendingChoice, target.Unit));
    }

    // 대상 선택 취소. 행동 재선택으로 복귀
    public void Back()
    {
        if (State != PresenterState.AwaitingTarget)
            throw new InvalidOperationException($"Back은 AwaitingTarget 상태에서만 가능(현재 {State})");

        _pendingChoice = null;
        TargetOptions = Array.Empty<TargetOptionVM>();
        State = PresenterState.AwaitingAction;
    }

    private void Commit(ActionCommand command)
    {
        Result = command;
        _pendingChoice = null;
        TargetOptions = Array.Empty<TargetOptionVM>();
        State = PresenterState.Committed;
    }

    // ActionChoice(+선택 대상) -> ActionCommand. Kind별 팩토리 매핑이 Presenter 출력 계약의 전부
    private ActionCommand Build(ActionChoice choice, BattleUnit target)
    {
        switch (choice.Kind)
        {
            case ActionKind.Skill:
                return ActionCommand.CreateSkill(Ally, choice.Skill, target);
            case ActionKind.UniqueAction:
                return ActionCommand.CreateUnique(Ally, choice.Skill, target);
            case ActionKind.Protection:
                return ActionCommand.CreateProtection(Ally, target);
            case ActionKind.Defense:
                return ActionCommand.CreateDefense(Ally, choice.Direction);
            case ActionKind.EndTurn:
                return ActionCommand.CreateEndTurn(Ally);
            default:
                // Item 등 ChoiceQuerySystem이 아직 산출하지 않는 종류. 도달 시 read layer 확장 신호
                throw new NotSupportedException($"AllyInputPresenter가 아직 못 다루는 Kind: {choice.Kind}");
        }
    }

    private static ActionOptionVM ToActionOptionVM(ActionChoice choice)
    {
        bool requiresTarget = choice.ValidTargets.Count > 0;

        // List<T> 분기와 Array.Empty<T>() 분기 사이엔 암묵적 변환이 없어 삼항연산자로 못 묶음: if/else로 대체
        IReadOnlyList<IncomingPreviewEntryVM> incoming;
        if (choice.Kind == ActionKind.Defense)
        {
            incoming = choice.IncomingPreviewDamages
                .Select(kv => new IncomingPreviewEntryVM(kv.Key, UnitLabel.Of(kv.Key), kv.Value.FinalDamage))
                .ToList();
        }
        else
        {
            incoming = Array.Empty<IncomingPreviewEntryVM>();
        }

        return new ActionOptionVM(choice.Kind, LabelFor(choice), choice.ApCost, requiresTarget, incoming, choice);
    }

    private static TargetOptionVM ToTargetOptionVM(ActionChoice choice, BattleUnit unit)
    {
        int? preview = choice.PreviewDamages.TryGetValue(unit, out DamageResult dr)
            ? dr.FinalDamage
            : (int?)null;
        return new TargetOptionVM(unit, UnitLabel.Of(unit), preview);
    }

    // 버튼 라벨
    // 여긴 SkillData.DisplayName을 그대로 읽을 뿐
    // 미입력(현재 플레이스홀더 에셋 상태) 시 SkillId로 대체(UnitLabel과 동일한 완화)
    private static string LabelFor(ActionChoice choice)
    {
        switch (choice.Kind)
        {
            case ActionKind.Skill:
            case ActionKind.UniqueAction:
                return string.IsNullOrEmpty(choice.Skill.DisplayName) ? choice.Skill.SkillId : choice.Skill.DisplayName;
            case ActionKind.Protection:
                return "보호";
            case ActionKind.Defense:
                return "방향방어 " + DirectionLabel(choice.Direction);
            case ActionKind.EndTurn:
                return "차례종료";
            default:
                return choice.Kind.ToString();
        }
    }

    private static string DirectionLabel(AttackDirection direction)
    {
        switch (direction)
        {
            case AttackDirection.High: 
                return "상단";
            case AttackDirection.Mid: 
                return "중단";
            case AttackDirection.Low:
                return "하단";
            default:
                return direction.ToString();
        }
    }
}