using System;
using System.Collections.Generic;

// 적 유닛. 붕괴/균열 게이지를 가진 유닛
public sealed class EnemyUnit : BattleUnit
{
    private int _currBreakOrCrackGauge;     // 단일 게이지. 일반+정예 = 붕괴, 보스 = 균열
    private readonly EnemyUnitData _data;

    public EnemyUnit(EnemyUnitData data) : base(data.BaseStats)
    { 
        _data = data;
        _currBreakOrCrackGauge = 0;
    }

    public int CurrBreakOrCrackGauge => _currBreakOrCrackGauge;
    public bool IsBoss => _data.IsBoss;
    public string EnemyId => _data.EnemyId;
    public string DisplayName => _data.DisplayName;
    public IReadOnlyList<SkillData> Skills => _data.Skills;
    public string BehaviorPatternID => _data.BehaviorPatternId;

    // 게이지 통로: 음수만 방지. 누적량, 발생조건, 초기화는 Break/CrackSystem 소유
    public void SetGauge(int value) => _currBreakOrCrackGauge = Math.Max(0, value);
}
