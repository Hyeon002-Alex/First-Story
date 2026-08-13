using System;
using System.Collections.Generic;

// 아군 유닛. AP를 가진 유닛
public sealed class AllyUnit : BattleUnit
{
    private int _currAP;
    private readonly AllyUnitData _data;    // 정적 정의 참조

    public AllyUnit(AllyUnitData data) : base(data.BaseStats)
    { 
        _data = data;
        _currAP = 0;    // 첫 턴 회복 전 0, min(+2, 6) 절삭 통일
    }

    public int CurrAP => _currAP;
    public string UnitId => _data.UnitId;
    public string DisplayName => _data.DisplayName;
    public IReadOnlyList<SkillData> EquippedSkills => _data.EquippedSkills;

    // AP 통로: 음수만 방지, 회복량, 소모, 판정은 APSystem 소유
    public void SetAP(int value) => _currAP = Math.Max(0, value);
}
