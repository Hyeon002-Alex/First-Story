using System.Collections.Generic;
using UnityEngine;

// 아군 캐릭터 한 명의 정적 정의. 전투 중 불변, 읽기 전용
[CreateAssetMenu(fileName = "Ally_", menuName = "Laplace/Ally Unit Data")]
public sealed class AllyUnitData : ScriptableObject
{
    [SerializeField] private string _unitId;
    [SerializeField] private string _displayName;

    [SerializeField] private SkillData _uniqueAction;                                    // 고유 행동. 직접 참조
    [SerializeField] private List<SkillData> _availableSkills = new List<SkillData>();   // 획득 가능 스킬. 직접 참조
    [SerializeField] private List<SkillData> _equippedSkills = new List<SkillData>();    // 장착 스킬. 정확히 3개
    [SerializeField] private UnitStats _baseStats;              // 인라인으로 인스펙터에 뜸

    public string UnitId => _unitId;
    public string DisplayName => _displayName;
    public SkillData UniqueAction => _uniqueAction;
    public IReadOnlyList<SkillData> AvailableSkills => _availableSkills;
    public IReadOnlyList<SkillData> EquippedSkills => _equippedSkills;
    public UnitStats BaseStats => _baseStats;
}
