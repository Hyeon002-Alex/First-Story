using System;

// 아군 결정 1건에 대한 요청/응답 쌍. 흐름이 입력 필요 지점에서 밖으로 내밀고
// 드라이버가 응답 슬롯을 채운 뒤 코루틴을 재개하면 흐름이 그 응답을 읽어 실행
// 우선 골격(단계/주체/응답 통로)만 세움
public sealed class InputRequest
{ 
    public InputPhase Phase { get; }
    public AllyUnit DecidingUnit { get; }

    // 응답 슬롯.코루틴은 MoveNext에 값을 넘기지 못하므로
    // 드라이버가 여길 채워 널는 것이 결정을 흐름으로 되돌리는 유일한 통로
    public ActionCommand Response { get; private set; }
    public bool IsAnswered => Response != null;

    public InputRequest(InputPhase phase, AllyUnit decidingUnit)
    {
        Phase = phase;
        DecidingUnit = decidingUnit ?? throw new ArgumentNullException(nameof(decidingUnit));
    }

    // 응답 통로. 도베인 경계 불변식만 강제
    // 명령 유효성(AP 지불 가능/대상 유효/단계 허용 종류)는 여기가 아닌 ResponsePhaseSystem 소유
    public void SetResponse(ActionCommand command)
    { 
        if(command == null)
            throw new ArgumentNullException(nameof(command));
        if (IsAnswered)
            throw new InvalidOperationException("이미 응답된 요청. 재응답 불가");
        if (command.Actor != DecidingUnit)
            throw new ArgumentException("응답 명령의 Actor가 요청 결정 주체와 다름", nameof(command));

        Response = command;
    }
}