// 행동 실행 구명. ActionResolver가 구현
// BattleFlowSystem은 명령서만 넘기고 실행 내부는 모름
public interface IActionExecutor
{
    void Execute(ActionCommand command);
}