using System.Collections.Generic;
using UnityEngine;

// 일반몹 한 패턴의 정적 정의. 우선순위 규칙 목록. 읽기 전용
// behaviorPatternId -> 이 매핑은 EnemyBehaviorSystem 레지스트리가 소유
// SO 자체는 자기 patternId만 소유
[CreateAssetMenu(fileName = "Behavior_", menuName = "Laplace/Behavior Pattern Data")]
public sealed class BehaviorPatternData : ScriptableObject
{
    [SerializeField] private string _patternId;     // 레지스트리 키. EnemyUnitData.BehaviorPatternId와 일치시킴
    [SerializeField] private List<BehaviorRule> _rules = new List<BehaviorRule>();

    public string PatternId => _patternId;
    public IReadOnlyList<BehaviorRule> Rules => _rules;
}