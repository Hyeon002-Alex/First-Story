using System;
using System.Collections.Generic;
using UnityEngine;

// 일반몹 결정. 순수함수: 진행상태 필드 없음
public sealed class DataDrivenBehavior : IEnemyBehavior
{
    private readonly BehaviorPatternData _pattern;  // 불변 참조

    public DataDrivenBehavior(BehaviorPatternData pattern)
    { 
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
    }

    public EnemyIntent Decide(BattleSnapshot snapshot, EnemyUnit self)
    {
        // 조건 primitive 추출
        float selfHPRatio = self.MaxHP > 0 ? self.CurrHP / (float)self.MaxHP : 0;
        int turnNum = snapshot.TurnNum;
        // "Ally" = 적 진영 = 적 목록
        // 자신 포함 전체 규모(자신 제외가 필요하면 -1 나중 조정)
        int livingAllyCount = snapshot.LivingEnemies.Count;

        foreach (BehaviorRule rule in _pattern.Rules)
        {
            if (!rule.Condition.Evaluate(selfHPRatio, turnNum, livingAllyCount))
                continue;

            SkillData skill = ResolveSkill(self, rule.SkillId);
            if (skill == null)
                continue;   // 데이터 오타 등으로 스킬 못 찾음 -> 이 규칙 무효, 다음 규칙으로

            BattleUnit target = ResolveTarget(skill, rule.TargetPolicy, snapshot, self);
            return new EnemyIntent(skill, target);
        }

        // 어느 규칙도 매치 못 함 = 폴백(Always) 규칙 누락. 데이터 결함을 로그로 드러냄
        Debug.LogWarning($"[G] {self.EnemyId} 패턴 {_pattern.PatternId}: 매치 규칙 없음(마지막 Always 폴백 누락?)");
        return null;
    }

    // skillId -> self가 실제 보유한 SkillData. 선형 탐색(일반몹 스킬 수 적음)
    private static SkillData ResolveSkill(EnemyUnit self, string skillId)
    {
        IReadOnlyList<SkillData> skills = self.Skills;
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] != null && skills[i].SkillId == skillId)
                return skills[i];
        }
        Debug.LogWarning($"[G] {self.EnemyId}: skillId '{skillId}' 미보유. 규칙 스킵");
        return null;
    }

    // 대상정책 -> 예정 대상. TargetRule이 형태를 가르고 정책은 "단일일 때 누구"만 답
    // Self는 대상 무관(실행 시 TargetingSystem이 actor로 강제). 그 외는 정책으로 플레이어 1명 산출
    // -> 그 1명이 Single이면 보호 리다이렉트 대상, Area면 진영 확장 대표, Fixed면 면역 대상.
    //    확장/리다이렉트/면역은 전부 실행 파이프(TargetingSystem) 소유. AI는 대표 1명만 정함
    private static BattleUnit ResolveTarget(SkillData skill, TargetPolicy policy, BattleSnapshot snapshot, EnemyUnit self)
    {
        if (skill.TargetRule == TargetRule.Self)
            return self;

        return TargetSelectionPolicy.Select(policy, snapshot.LivingAllies);
    }
}
