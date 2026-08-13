using System.Collections.Generic;
using UnityEngine;

// 적 한 종류의 정적 정의. 전투 중 불변, 읽기 전용
// 일반, 정예는 MaxBreakGuage, 보스는 MaxCrackGuage를 사용
[CreateAssetMenu(fileName = "Enemy_", menuName = "Laplace/Enemy Unit Data")]
public sealed class EnemyUnitData : ScriptableObject
{
    [SerializeField] private string _enemyId;
    [SerializeField] private string _displayName;

    [SerializeField] private bool _isBoss;
    [SerializeField] private int _maxBreakGauge;
    [SerializeField] private int _maxCrackGauge;

    [SerializeField] private List<SkillData> _skills = new List<SkillData>();        // 사용 스킬
    [SerializeField] private List<string> _statusImmunityIds = new List<string>();  // 상태이상 면역. StatusEffectData 생기면 참조화
    [SerializeField] private string _behaviorPatternId;                             // AI 팩토리 키. 가리킬 단일 SO 없음. 문자열 유지
    [SerializeField] private UnitStats _baseStats;

    public string EnemyId => _enemyId;
    public string DisplayName => _displayName;
    public bool IsBoss => _isBoss;
    public int MaxBreakGauge => _maxBreakGauge;
    public int MaxCrackGauge => _maxCrackGauge;
    public IReadOnlyList<SkillData> Skills => _skills;
    public IReadOnlyList<string> StatusImmunityIds => _statusImmunityIds;
    public string BehaviorPatternId => _behaviorPatternId;
    public UnitStats BaseStats => _baseStats;
}
