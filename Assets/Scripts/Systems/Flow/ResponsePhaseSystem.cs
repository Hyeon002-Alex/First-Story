using System;
using UnityEngine;

// 6단계 방어대응의 두 선언을 검증, 적용
// 이 둘은 스킬이 없어 ActionResolver가 의도적으로 실행하지 않는 행동
// 정보확인, 스킬계열 대응은 이 시스템 밖
// 여기는 방향방어, 보호만
public static class ResponsePhaseSystem
{
    // 스킬 없는 고정 대응행동의 AP 비용. 밸런스 값 -> 데이터 자산 이관 대상
    private static readonly int _defenseAPCost = 1;
    private static readonly int _protectionAPCost = 1;

    // 비용 읽기 노출: 선택지 산출이 이 값을 복제하지 않고 이 단일 소유를 읽음
    public static int DefenseAPCost => _defenseAPCost;
    public static int ProtectionAPCost => _protectionAPCost;

    // 이 종류가 방어대응단계 소관인가. "유효 종류 집합"의 단일 소유
    // 스텝6 라우팅과 TryApply의 switch가 같은 집합을 봐야 함
    // 샹후 방어대응가능 스킬(도발 등) 추가 시 이 메서드와 아래 switch만 함께 확장
    public static bool IsResponseKind(ActionKind kind)
        => kind == ActionKind.Defense || kind == ActionKind.Protection;

    // 대응 명령 1건 검증, 적용. 적용 true / AP부족, 대상무효 거부 false
    // 명령의 형태는 ActionCommand 팩토리가 이미 보증
    // 여기선 런타임 상태만 게이트함
    public static bool TryApply(ActionCommand command, ProtectionSystem protection)
    { 
        if(command == null)
            throw new ArgumentNullException(nameof(command));
        if(protection == null) 
            throw new ArgumentNullException(nameof(protection));

        // 대응 주체는 아군만. 방어적 확인
        if (!(command.Actor is AllyUnit ally))
            throw new ArgumentException("대응행동 주체는 AllyUnit이어야 함", nameof(command));

        switch (command.Kind)
        {
            case ActionKind.Defense:
                return ApplyDefense(ally, command.Direction);
            case ActionKind.Protection:
                return ApplyProtection(ally, command.Target, protection);
            default:
                // Reveal/스킬/endTurn 등은 이 시스템 소관이 아님
                throw new ArgumentException(
                    $"ResponsePhaseSystem 미처리 종류: {command.Kind} (방향방어·보호만 처리)", nameof(command));
        }
    }

    private static bool ApplyDefense(AllyUnit ally, AttackDirection direction)
    {
        if (!APSystem.CanAfford(ally, _defenseAPCost))
        {
            Debug.Log($"[대응 거부] {ally.UnitId} 방향방어 AP부족 ({ally.CurrAP}/{_defenseAPCost})");
            return false;
        }

        // 아군 방어: 방향방어 지정, 약점 None(아군은 약점 없음). 1턴 한정
        ally.SetStance(direction, AttackDirection.None);
        APSystem.Consume(ally, _defenseAPCost);
        ally.SetActed(true);    // 대응행동한 유닛은 재행동하지 않음
        Debug.Log($"[대응] {ally.UnitId} 방향방어 {direction} (AP {ally.CurrAP})");
        return true;
    }

    private static bool ApplyProtection(AllyUnit ally, BattleUnit target, ProtectionSystem protection)
    {
        // 자기보호 금지. SetProtect도 확인하지만 여기서 거부
        if (ReferenceEquals(ally, target))
        {
            Debug.Log($"[대응 거부] {ally.UnitId} 보호: 자기 자신 지정 불가");
            return false;
        }
        // 대상 = 생존 아군
        if (!(target is AllyUnit alliedTarget) || alliedTarget.IsIncapacitated)
        {
            Debug.Log($"[대응 거부] {ally.UnitId} 보호 대상 무효(전투불능/비아군)");
            return false;
        }
        if (!APSystem.CanAfford(ally, _protectionAPCost))
        {
            Debug.Log($"[대응 거부] {ally.UnitId} 보호 AP부족 ({ally.CurrAP}/{_protectionAPCost})");
            return false;
        }

        protection.SetProtect(ally, alliedTarget);
        APSystem.Consume(ally, _protectionAPCost);
        ally.SetActed(true);
        Debug.Log($"[대응] {ally.UnitId} 보호 -> {alliedTarget.UnitId} (AP {ally.CurrAP})");
        return true;
    }
}