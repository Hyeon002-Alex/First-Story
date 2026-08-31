using System;

// 방향방어 오퍼 하나를 고르면 맞을 예상피해 1건. 공격자별로 여러 건 붙을 수 있음
// -> 같은 아군을 여러 적이 동시에 노릴 때. ActionOptionVM.IncomingPreview가 이 목록을 들고 있음
public sealed class IncomingPreviewEntryVM
{ 
    public BattleUnit Attacker { get; }
    public string AttackerLabel { get; }
    public int FinalDamager { get; }

    public IncomingPreviewEntryVM(BattleUnit attacker, string attackerLabel, int finalDamager)
    {
        Attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
        AttackerLabel = attackerLabel;
        FinalDamager = finalDamager;
    }
}