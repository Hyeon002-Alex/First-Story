// 피해 적용 분해값. 보호막이 얼마를 흡수하고, HP가 얼마를 잃었는지
// 흡수 계산은 DamageSystem 한 곳에만 존재
public readonly struct DamageApplication
{
    public int ShieldAbsorbed { get; }
    public int HPLost { get; }

    public DamageApplication(int shieldAbsorbed, int hpLost)
    {
        ShieldAbsorbed = shieldAbsorbed;
        HPLost = hpLost;
    }
}