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
    public int MaxBreakGauge => _data.MaxBreakGauge;    // 일반, 정예 붕괴 임계
    public int MaxCrackGauge => _data.MaxCrackGauge;    // 보스 균열 임계
    public string EnemyId => _data.EnemyId;
    public string DisplayName => _data.DisplayName;
    public IReadOnlyList<SkillData> Skills => _data.Skills;
    public string BehaviorPatternID => _data.BehaviorPatternId;

    // 상태이상 면역 목록. 면역 판정은 StatusEffectSystem 소유, 여긴 데이터 노출만
    public IReadOnlyList<StatusEffectData> StatudImmunities => _data.StatusImmunities;

    // 적 자세 = 패시브. 불일치 시 1.00 무보상. 자세 부여 스킬은 v0.1.0 미사용
    protected override bool StanceIsActive => false;

    // 게이지 통로: 음수만 방지. 누적량, 발생조건, 초기화는 Break/CrackSystem 소유
    public void SetGauge(int value) => _currBreakOrCrackGauge = Math.Max(0, value);
}
