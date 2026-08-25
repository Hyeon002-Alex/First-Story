using System;
using UnityEngine;

// 5단계 정보대응의 정보형 고유행동을 검증, 적용
// 방향방어/보호와 달리 스킬 기반이라 별도 시스템으로 분리
// 정보형 고유행동 = UniqueAction + SkillData.IsInfoAction
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

    // 정보형 고유행동 1건 검증, 적용. 적용 true / AP부족, 대상무효 false
    // intentSystem 주입: 공개 플래그인 _reavealed는 IntentSystem이 소유
    public static bool TryApply(ActionCommand command, IntentSystem intentSystem)
    { 
        if(command == null)
            throw new ArgumentNullException(nameof(command));
        if(intentSystem == null)
            throw new ArgumentNullException(nameof(intentSystem));

        // 주체는 아군만. 방어적 확인
        if(!(command.Actor is AllyUnit ally))
            throw new ArgumentException("정보대응 주체는 AllyUnit이어야 함", nameof(command));

        // 스코프 밖 명령은 프로그래밍 오류. 스텝5가 IsInfoResponse로 걸러 넘김
        if (!IsInfoResponse(command))
            throw new ArgumentException(
                $"InfoResponseSystem 미처리 명령: Kind={command.Kind} (정보형 고유행동만 처리)", nameof(command));

        // 대상 = 생존 적, EnemyUnit
        if (!(command.Target is EnemyUnit enemy) || enemy.IsIncapacitated)
        {
            Debug.Log($"[정보대응 거부] {ally.UnitId} 대상 무효(전투불능/비적)");
            return false;
        }

        // AP 비용은 스킬 apCost. 스킬이 값 소유
        int apCost = command.Skill.ApCost;
        if (!APSystem.CanAfford(ally, apCost))
        {
            Debug.Log($"[정보대응 거부] {ally.UnitId} 정보확인 AP부족 ({ally.CurrAP}/{apCost})");
            return false;
        }

        // 공개 게이트 ON + AP 소모 + 행동 소진
        intentSystem.SetRevealed(enemy);
        APSystem.Consume(ally, apCost);
        ally.SetActed(true);

        // 캐릭터 스킬 설계 후 수렴: 공격 겸용 확정. 실행(ActionResolver.Execute)은 여기 아님
        // -> Step5_InfoResponse가 이 메서드 성공 후 executor로 직접 호출
        Debug.Log($"[정보대응] {ally.UnitId} 정보확인 -> {enemy.EnemyId} (AP {ally.CurrAP})");
        return true;
    }
}