using System;
using System.Collections.Generic;

// 방어위상 예상피해 산출. 순수 조회. 상태 안 바꿈
// 이 아군이 direction을 고르는 전재 하에, 이 아군을 노리는 공격자별 예상 DamageResult
public static class DefensePreviewSystem
{
    // ally = 방어위상 입력중인 아군. direction = 후보 방향, None 불가
    // intentSytem/enemies = GetAttackers 호출에 그대로 위임
    public static IReadOnlyDictionary<EnemyUnit, DamageResult> Preview(
        AllyUnit ally, AttackDirection direction, IntentSystem intentSystem, IReadOnlyList<EnemyUnit> enemies)
    {
        if (ally == null)
            throw new ArgumentNullException(nameof(ally));
        if (direction == AttackDirection.None)
            throw new ArgumentException("방어위상 프리뷰는 High/Mid/Low만", nameof(direction));
        if (intentSystem == null)
            throw new ArgumentNullException(nameof(intentSystem));
        if (enemies == null)
            throw new ArgumentNullException(nameof(enemies));

        var result = new Dictionary<EnemyUnit, DamageResult>();

        // 미확인 제외/범위 소속은 GetAttacker가 이미 완료. 여긴 계산만
        IReadOnlyList<EnemyUnit> attackers = intentSystem.GetAttackers(ally, enemies);

        // 가상 자세: 아군 기준 조립. 약점방향 항상 None( 아군 특성) + 능동(IsActive=true, 불일치 0.75)
        // "가상"이라는 사실은 이 자세 값 자체가 아니라 호출 맥락(아직 확정 안 됨)에만 있음 -> 구조체는 몰라도 됨
        var hypotheticalStance = new DefenseStance(direction, AttackDirection.None, isActive: true);

        foreach (EnemyUnit enemy in attackers)
        {
            SkillData skill = intentSystem.GetIntent(enemy).Skill;   // GetAttackers 통과 = intent 존재 보장

            // 피해 없는 스킬(회복/디버프 등)은 방향방어 프리뷰 대상 아님. HasDamage 단일 소유 재사용
            if (!ActionResolver.HasDamage(skill))
                continue;

            // actor=enemy(공격자), target=ally(방어자, 프리뷰 대상) -> 4인자 오버로드
            result[enemy] = ActionResolver.PreviewDamage(enemy, ally, skill, hypotheticalStance);
        }

        return result;
    }
}