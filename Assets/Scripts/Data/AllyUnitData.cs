using System.Collections.Generic;
using UnityEngine;

// 아군 캐릭터 한 명의 정적 정의. 전투 중 불변, 읽기 전용
[CreateAssetMenu(fileName = "Ally_", menuName = "Laplace/Ally Unit Data")]
public sealed class AllyUnitData : ScriptableObject
{
    [SerializeField] private string _unitId;

    [SerializeField] private string _uniqueActionId;            // 고유 행동 Id
    [SerializeField] private List<string> _availableSkillIds = new List<string>();   // 획득 가능 스킬 Id
    [SerializeField] private List<string> _equippedSkillIds = new List<string>();    // 장착 스킬. 정확히 3개
    [SerializeField] private UnitStats _baseStats;              // 인라인으로 인스펙터에 뜸

    public string UnitId => _unitId;
    public string UniqueActionId => _uniqueActionId;
    public IReadOnlyList<string> AvailableSkillIds => _availableSkillIds;
    public IReadOnlyList<string> EquippedSkillIds => _equippedSkillIds;
    public UnitStats BaseStats => _baseStats;
}
