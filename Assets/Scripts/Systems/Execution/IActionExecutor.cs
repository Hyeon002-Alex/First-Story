// 행동 실행 구명. ActionResolver가 구현
// BattleFlowSystem은 명령서만 넘기고 실행 내부는 모름
// currentTurn: 붕괴 받는 피해증가 만료턴 계상, 상태이상 지연틱 기준에도 필요
public interface IActionExecutor
{
    void Execute(ActionCommand command, int currtentTurn);
}