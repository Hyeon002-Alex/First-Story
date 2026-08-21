using UnityEngine;

// 보스 behavior 공통 골격. IEnemyBehavior 구현 -> 호출부(EnemyBehaviorSystem)는 잡몹과 구별 못 함
// phaseIndex·sequenceStep을 여기(behavior 내부 필드)에 가둠. EnemyUnit 런타임엔 범용 필드 안 붙임
// -> 결정 로직 진행상태가 유닛으로 새지 않음. 유닛은 얇게 유지
// 페이즈 전환 = 판단만. 게이지·자세 리셋 실행은 묶음3 별 경로 재사용(여기서 리셋 안 함)
// v0.1.0 공개범위(프롤로그~4장)에 페이즈 보스 없음. 계약·상태 원칙까지만 확정
// 실제 페이즈·시퀀스 로직은 보스 실물 등장 시 파생 클래스(Chapter17System 등)가 채움
// 실물 보스가 오면 이 골격을 상속해 Decide/페이즈 판단 구현
public abstract class BossBehaviorBase : IEnemyBehavior
{
    // 파생 클래스만 읽고 씀. 일반몹엔 이 필드 자체가 없음
    // 저장 스코프 대상: 전투 중 저장->재개 시 복원돼야 시퀀스 재현(결정론 유지 조건)
    protected int _phaseIndex;      // 현재 페이즈. 0부터
    protected int _sequenceStep;    // 페이즈 내 시퀀스 진행(충전->강타 등 stateful 표현)

    protected BossBehaviorBase()
    {
        _phaseIndex = 0;
        _sequenceStep = 0;
    }

    // 잡몹과 같은 계약. 파생 보스가 페이즈·시퀀스 기반 결정을 구현
    // 기본 구현은 미제공(abstract) — 실물 보스마다 로직이 달라 공통 기본값이 무의미
    public abstract EnemyIntent Decide(BattleSnapshot snapshot, EnemyUnit self);

    // 페이즈 전환 판단만. "지금 올릴 때인가"(HP 임계/사건)를 파생이 판정
    // 반환 true 시 호출측(보스 전용 시스템)이 리셋 별 경로 + _phaseIndex 증가 실행
    // 기본 false — 페이즈 없는 보스도 계약 만족. 실물이 오버라이드
    protected virtual bool ShouldAdvancePhase(BattleSnapshot snapshot, EnemyUnit self) => false;

    // === 저장/복원 훅 === //
    // 내부 상태 직렬화 스코프. save/load 설계 시 이 훅으로 phaseIndex/sequenceStep 왕복
    public virtual void CaptureState(out int phaseIndex, out int sequenceStep)
    {
        phaseIndex = _phaseIndex;
        sequenceStep = _sequenceStep;
    }

    public virtual void RestoreState(int phaseIndex, int sequenceStep)
    {
        _phaseIndex = phaseIndex;
        _sequenceStep = sequenceStep;
    }
}