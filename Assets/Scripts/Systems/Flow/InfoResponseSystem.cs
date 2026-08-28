using System;
using UnityEngine;

// 5단계 정보대응의 정보형 고유행동을 검증. 순수 판정
// reveal/피해/붕괴/AP/acted 순차 적용은 Step5_InfoResponse가 검증 통과 후 직접 오케스트레이션
public static class InfoResponseSystem
{
    // 정보형 스킬 자격 단일 소유: 이 플래그가 정보형 대응 자격의 진실원
    // 제안과 집행이 같은 기준을 여기서 읽어 드리프트 차단
    public static bool IsInfoActionSkill(SkillData skill)
        => skill != null && skill.IsInfoAction;

    // 이 명령이 정보대응 단계 소관인가
    public static bool IsInfoResponse(ActionCommand command)
        => command != null
        && command.Kind == ActionKind.UniqueAction
        && IsInfoActionSkill(command.Skill);

    // 정보형 고유행동 1건 자격 검증만. 상태 변경 없음
    public static bool TryValidate(
        ActionCommand command, out AllyUnit ally, out EnemyUnit enemy, out int apCost)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (!(command.Actor is AllyUnit a))
            throw new ArgumentException("정보대응 주체는 AllyUnit이어야 함", nameof(command));
        if (!IsInfoResponse(command))
            throw new ArgumentException(
                $"InfoResponseSystem 미처리 명령: Kind={command.Kind} (정보형 고유행동만 처리)", nameof(command));

        ally = a;
        apCost = command.Skill.ApCost;
        enemy = null;

        if (!(command.Target is EnemyUnit e) || e.IsIncapacitated)
        {
            Debug.Log($"[정보대응 거부] {ally.UnitId} 대상 무효(전투불능/비적)");
            return false;
        }
        if (!APSystem.CanAfford(ally, apCost))
        {
            Debug.Log($"[정보대응 거부] {ally.UnitId} 정보확인 AP부족 ({ally.CurrAP}/{apCost})");
            return false;
        }

        enemy = e;
        return true;
    }
}