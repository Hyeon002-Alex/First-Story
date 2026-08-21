using System;
using System.Collections.Generic;

// 미리 계획된 명령 순서를 그대로 재생하는 결정론적 입력 소스
// 프로브가 아군 결정을 고정 주입해 전투 전 경로를 검증하는 용도
// 소진 시 차례종료 반환
public sealed class ScriptedInputSource : IPlayerInputSource
{
    private readonly Queue<ActionCommand> _script;

    public ScriptedInputSource(IEnumerable<ActionCommand> script)
    {
        // null 스크립트 = 빈 큐. 모든 요청에 차례종료로 응답
        _script = script != null ? new Queue<ActionCommand>(script) : new Queue<ActionCommand>();
    }

    public ActionCommand Resolve(InputRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // 큐 소진 시 요청 주체의 차례종료. 단계 무관 안전 무행동
        if (_script.Count == 0)
            return ActionCommand.CreateEndTurn(request.DecidingUnit);

        return _script.Dequeue();
    }
}
