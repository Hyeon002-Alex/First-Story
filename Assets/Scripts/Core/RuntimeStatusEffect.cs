using System;

// 유닛에 실제로 붙는 상테이상 인스턴스. RuntimeStats 상태이상목록의 원소
// 정의는 정적 참조, 인스턴스별 상태만 여기 보관
public sealed class RuntimeStatusEffect
{ 
    public StatusEffectData Definition { get; }
    public int AppliedTurn { get; }                     // 지연틱 판정 기준
    public int RemainingDuration { get; private set; }
    public int DamageSnapShot { get; private set; }     // 지속피해 조각. 없으면 0

    public RuntimeStatusEffect(StatusEffectData definition, int appliedTurn, int remainingDuration, int damageSnapShot)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        AppliedTurn = appliedTurn;
        RemainingDuration = remainingDuration;
        DamageSnapShot = damageSnapShot;
    }

    // 중첩 갱신: 합산 안 함. 더 긴 지속 / 더 강한 스냅샷 유지
    public void RefreshDuration(int newDuration) => RemainingDuration = Math.Max(RemainingDuration, newDuration);
    public void RefreshSnapShot(int newSnapShot) => DamageSnapShot = Math.Max(DamageSnapShot, newSnapShot);

    // 지속감소, 만료, 지연틱 통로. 통로만 심고 실제 호출은 8단계에서
    public void Decrement() => RemainingDuration--;
    public bool IsExpired => RemainingDuration <= 0;
    public bool IsTickable(int currentTurn) => currentTurn > AppliedTurn;   // 적용 다음 턴부터
}