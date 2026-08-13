using System;

// 전투 중 변하는 상태 값 묶음. 유닛 하나가 하나를 가짐
// 전투 재시작 시 이것만 새로 만듦. 순수 객체
public sealed class RuntimeStats
{
    private readonly int _maxHP;    // HP 클램프 상한. 정적 최대 HP 복사
    private int _currHP;
    
    private int _shield;        // 보호막량
    private int _evasionCount;  // 회피 횟수. 최대 3
    private float _damageTakenMod;          // 받는 피해 증가 배율. 기본 1.00, 붕괴/균열 시 1.50
    private int _damageTakenModExpireTurn;  // 만로 글로벌 턴. 비활성 시 0
    private AttackDirection _defenseDirection;      // 방어 방향. None = 자세 없음
    private AttackDirection _weaknessDirection;     // 약점 방향. 아군은 항상 None

    // 현재 HP를 최대 Hp에서 출발시킴. 보호막, 회피는 0
    public RuntimeStats(int maxHP)
    { 
        _maxHP = maxHP;
        _currHP = maxHP;
        _shield = 0;
        _evasionCount = 0;
        _damageTakenMod = 1.00f;
        _damageTakenModExpireTurn = 0;
        _defenseDirection = AttackDirection.None;
        _weaknessDirection = AttackDirection.None;
    }

    public int CurrHP => _currHP;
    public int Shield => _shield;
    public int EvasionCount => _evasionCount;
    public float DamageTakenMod => _damageTakenMod;
    public int DamageTakenModExpireTurn => _damageTakenModExpireTurn;
    public AttackDirection DefenseDirection => _defenseDirection;
    public AttackDirection WeaknessDirection => _weaknessDirection;

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
    public void SetDamageTakenMod(float mod, int expireTurn)
    { 
        _damageTakenMod = Math.Max(0f, mod);
        _damageTakenModExpireTurn = expireTurn;
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
}