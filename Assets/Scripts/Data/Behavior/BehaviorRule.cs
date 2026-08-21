using System;
using UnityEngine;

// 일반몹 우선순위 규칙 한 줄. 조건 매치 시 이 스킬 + 대상정책 채택
// 위->아래 첫 매치라 순서가 결정혼을 줌
[Serializable]
public sealed class BehaviorRule
{
    [SerializeField] private BehaviorCondition _condition;
    [SerializeField] private string _skillId;   // enemy.Skills에서 SkillId로 조회할 키
    [SerializeField] private TargetPolicy _targetPolicy;

    public BehaviorCondition Condition => _condition;
    public string SkillId => _skillId;
    public TargetPolicy TargetPolicy => _targetPolicy;

    // Unity 직렬화용 기반 생성자
    public BehaviorRule() { }

    // 코드 생성용. 프로브/테스트
    public BehaviorRule(BehaviorCondition condition, string skillId, TargetPolicy targetPolicy)
    { 
        _condition = condition;
        _skillId = skillId;
        _targetPolicy = targetPolicy;
    }
}