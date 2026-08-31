using System;

// 대상 선택 단계의 후보 1명
// PreviewDamage는 int?(nullable): 0도 유효한 계산 결과라 피해없음/미계산 과 구분해야 함
// null = 피해 없는 스킬이거나 ActionChoice.PreviewDamages에 대상 항목이 없음
public sealed class TargetOptionVM
{
    public BattleUnit Unit { get; }
    public string Label { get; }
    public int? PreviewDamage { get; }

    public TargetOptionVM(BattleUnit unit, string label, int? previewDamage)
    {
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        Label = label;
        PreviewDamage = previewDamage;
    }
}