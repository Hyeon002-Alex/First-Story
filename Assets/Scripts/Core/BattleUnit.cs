// 아군, 적 공통 부모. HP가 있고 피격되는 물체의 공통 런타임 클래스
public abstract class BattleUnit
{
    protected readonly UnitStats _baseStats;    // 정적 기본 스탯 참조. 자식 _data에서 넘어옴
    protected readonly RuntimeStats _runtime;   // 런타임 상태 묶음
    private bool _isIncapacitated;
    private bool _actedThisTurn;                // 이번 글로벌 턴 행동 완료. 붕괴 행동취소 판정 + 7단계 재검사

    protected BattleUnit(UnitStats baseStats)
    { 
        _baseStats = baseStats;
        _runtime = new RuntimeStats(baseStats.MaxHP);   // 현재 HP = 최대 HP로 시작
    }

    // === 읽기: 현재 상태 === //
    public int MaxHP => _baseStats.MaxHP;
    public int CurrHP => _runtime.CurrHP;
    public int Shield => _runtime.Shield;
    public int EvasionCount => _runtime.EvasionCount;
    public bool IsIncapacitated => _isIncapacitated;
    public bool ActedThisTurn => _actedThisTurn;
    public float DamageTakenMod => _runtime.DamageTakenMod;
    public int DamageTakenModExpireTurn => _runtime.DamageTakenModExpireTurn;

    // === 읽기: 유효 스탯. 단일 경로, 기본 + 보정, 보정 = 일시강화 - 상태 파생, 지금은 0 === //
    public int EffectiveAttack => Clamp0(_baseStats.Attack + AttackModifier);
    public int EffectiveDefense => Clamp0(_baseStats.Defense + DefenseModifier);
    public int EffectiveSpeed => Clamp0(_baseStats.Speed + SpeedModifier);

    // 보정원. 지금은 0
    private int AttackModifier => 0;
    private int DefenseModifier => 0;
    private int SpeedModifier => 0;

    // === 저수준 쓰기 통로. RuntimeStats 위임. 자기 플래그 === //
    public void ModifyHP(int value) => _runtime.ModifyHP(value);
    public void SetShield(int value) => _runtime.SetShield(value);
    public void SetEvasion(int value) => _runtime.SetEvasion(value);
    public void SetIncapacitated(bool value) => _isIncapacitated = value;
    public void SetActed(bool value) => _actedThisTurn = value;
    public void SetDamageTakenMod(float mod, int expireTurn)
        => _runtime.SetDamageTakenMod(mod, expireTurn);

    // 유효 스탯 하한 0
    private static int Clamp0(int v) => v < 0 ? 0 : v;
}