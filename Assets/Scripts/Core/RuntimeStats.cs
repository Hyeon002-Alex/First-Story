using System;
using System.Collections.Generic;

// 전투 중 변하는 상태 값 묶음. 유닛 하나가 하나를 가짐
// 전투 재시작 시 이것만 새로 만듦. 순수 객체
public sealed class RuntimeStats
{
    private readonly int _maxHP;    // HP 클램프 상한. 정적 최대 HP 복사
    private int _currHP;
    
    private int _shield;        // 보호막량
    private int _evasionCount;  // 회피 횟수. 최대 3

    // 붕괴/균열 전용 받는피해 배율. 상태이상 받는피해증가와 분리
    private float _breakDamageMod;         
    private int _breakDamageModExpireTurn; 

    private AttackDirection _defenseDirection;      // 방어 방향. None = 자세 없음
    private AttackDirection _weaknessDirection;     // 약점 방향. 아군은 항상 None

    // 붙어있는 상태이상 인스턴스
    private readonly List<RuntimeStatusEffect> _statusEffects = new List<RuntimeStatusEffect>();

    // 현재 HP를 최대 Hp에서 출발시킴. 보호막, 회피는 0
    public RuntimeStats(int maxHP)
    { 
        _maxHP = maxHP;
        _currHP = maxHP;
        _shield = 0;
        _evasionCount = 0;
        _breakDamageMod = 1.00f;
        _breakDamageModExpireTurn = 0;
        _defenseDirection = AttackDirection.None;
        _weaknessDirection = AttackDirection.None;
    }

    public int CurrHP => _currHP;
    public int Shield => _shield;
    public int EvasionCount => _evasionCount;
    public float BreakDamageMod => _breakDamageMod;
    public int BreakDamageModExpireTurn => _breakDamageModExpireTurn;
    public AttackDirection DefenseDirection => _defenseDirection;
    public AttackDirection WeaknessDirection => _weaknessDirection;

    // 상태이상 목록 읽기. 밖에서 Add/Remove 불가
    public IReadOnlyList<RuntimeStatusEffect> StatusEffects => _statusEffects;

    // 저수준 쓰기 통로. 최소 불변식만
    // HP 도메인 [0, maxHP] 클램프. value는 DamageSystem, HealingSystem이 계산
    public void ModifyHP(int value)
    { 
        int next = _currHP + value;
        _currHP = Math.Max(0, Math.Min(_maxHP, next));
    }

    // shield, evasion = 음수만 방지. 상한은 각 시스템 소유
    public void SetShield(int value) => _shield = Math.Max(0, value);
    public void SetEvasion(int value) => _evasionCount = Math.Max(0, value);

    // 받는 피해 증가 통로: 배율 음수 방지. 만료턴 규칙은 Break/CrackSystem 소유
    public void SetBreakDamageMod(float mod, int expireTurn)
    {
        _breakDamageMod = Math.Max(0f, mod);
        _breakDamageModExpireTurn = expireTurn;
    }

    // 방향 통로: 방향값 저장만. 조립(DefenseStance)/IsActive/만료 규칙은 상위 소유
    public void SetStance(AttackDirection defenseDir, AttackDirection weaknessDir)
    { 
        _defenseDirection = defenseDir;
        _weaknessDirection = weaknessDir;
    }

    public void ClearStance()
    { 
        _defenseDirection = AttackDirection.None;
        _weaknessDirection= AttackDirection.None;
    }

    // === 상태이상 목록 통로. 규칙은 StatusEffectSystem이 소유. 여긴 저장만 === //
    public void AddStatusEffect(RuntimeStatusEffect effect)
    { 
        if(effect == null)
            throw new ArgumentNullException(nameof(effect));

        _statusEffects.Add(effect);
    }

    public void RemoveStatusEffect(RuntimeStatusEffect effect) => _statusEffects.Remove(effect);

    // 중첩 조회: 동일 statusID 인스턴스 반환. 없으면 null
    public RuntimeStatusEffect FindStatusEffect(string statusId)
    {
        foreach (RuntimeStatusEffect e in _statusEffects)
        { 
            if(e.Definition.StatusId == statusId)
                return e;
        }
        return null;
    }

    // === 파생 집계. 집계는 Core 소유. 유닛 접근자와 System 소비처가 이 하나를 공유 === //
    // 합산형
    public float SumStatusMag(EffectKind kind)
    {
        float sum = 0f;
        foreach (EffectComponent c in ActiveComponents(kind))
        {
            sum += c.Magnitude;
        }
        return sum;
    }

    // 곱셈형: 없으면 기본 1.0
    public float ProductStatusMag(EffectKind kind)
    {
        float product = 1f;
        foreach (EffectComponent c in ActiveComponents(kind))
        { 
            product *= c.Magnitude;
        }
        return product;
    }

    // 존재형
    public bool HasStatusComponent(EffectKind kind)
    {
        foreach (EffectComponent c in ActiveComponents(kind))
        {
            return true;
        }
        return false;
    }

    // 활성 목록에서 특정 effectKind만 훑음
    private IEnumerable<EffectComponent> ActiveComponents(EffectKind kind)
    {
        foreach (RuntimeStatusEffect e in _statusEffects)
        {
            foreach (EffectComponent c in e.Definition.Components)
            {
                if (c.EffectKind == kind)
                    yield return c;
            }
        }
    }
}