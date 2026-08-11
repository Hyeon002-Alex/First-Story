using System.Collections.Generic;
using UnityEngine;

// 적 한 종류의 정적 정의. 전투 중 불변, 읽기 전용
// 일반, 정예는 MaxBreakGuage, 보스는 MaxCrackGuage를 사용
[CreateAssetMenu(fileName = "Enemy_", menuName = "Laplace/Enemy Unit Data")]
public sealed class EnemyUnitData : ScriptableObject
{
    [SerializeField] private string _enemyId;

    [SerializeField] private bool _isBoss;
    [SerializeField] private int _maxBreakGauge;
    [SerializeField] private int _maxCrackGauge;

    [SerializeField] private List<string> _skillIds = new List<string>();
    [SerializeField] private List<string> _statusImmunityIds = new List<string>();
    [SerializeField] private string _behaviorPatternId;
    [SerializeField] private UnitStats _baseStats;

    public string EnemyId => _enemyId;
    public bool IsBoss => _isBoss;
    public int MaxBreakGuage => _maxBreakGauge;
    public int MaxCrackGuage => _maxCrackGauge;
    public IReadOnlyList<string> SkillIds => _skillIds;
    public IReadOnlyList<string> StatusImmunityIds => _statusImmunityIds;
    public string BehaviorPatternId => _behaviorPatternId;
    public UnitStats BaseStats => _baseStats;
}
