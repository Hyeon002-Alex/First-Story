// 피해 계산 결과
// 순수값 -> readonly struct. 적용여부는 UI가 mod != 1f로 파생
public readonly struct DamageResult
{ 
    public int FinalDamage { get; }
    public float DirectionMod { get; }  // 방향방어 계수
    public float BreakMod { get; }      // 적 전용 붕괴 계스

    public DamageResult(int finalDamage, float directionMod, float breakMod)
    {
        FinalDamage = finalDamage;
        DirectionMod = directionMod;
        BreakMod = breakMod;
    }
}