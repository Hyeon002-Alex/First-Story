using System;
using UnityEngine;

// 일반몹 규칙 한 줄의 발동 조건
// 레이어 유지: Evaluate가 Core가 아니라 미리 뽑은 primitive만 받음
//              Data->Core 역참조 없음
// 순수 함수. 같은 입력 -> 항상 같은 결과. 무작위 없음
[Serializable]
public struct BehaviorCondition
{
    [SerializeField] private ConditionKind _kind;
    [SerializeField] private float _threshold;  // 비율 조건용(SelfHPBelow)
    [SerializeField] private int _intA;         // 정수 조건용1(나누는 수/턴 임계/아군 수)
    [SerializeField] private int _intB;         // 정수 조건용2(나머지)

    public ConditionKind Kind => _kind;

    // 코드 생성용 팩토리. 프로브/테스트 용
    public BehaviorCondition(ConditionKind kind, float threshold, int intA, int intB)
    { 
        _kind = kind;
        _threshold = threshold;
        _intA = intA;
        _intB = intB;
    }

    public static BehaviorCondition CreateAlways()
       => new BehaviorCondition(ConditionKind.Always, 0f, 0, 0);
    public static BehaviorCondition CreateSelfHpBelow(float ratio)
        => new BehaviorCondition(ConditionKind.SelfHPBelow, ratio, 0, 0);
    public static BehaviorCondition CreateTurnNumberMod(int divisor, int remainder)
        => new BehaviorCondition(ConditionKind.TurnNumberMod, 0f, divisor, remainder);
    public static BehaviorCondition CreateTurnNumberAtLeast(int turn)
        => new BehaviorCondition(ConditionKind.TurnNumberAtLeast, 0f, turn, 0);
    public static BehaviorCondition CreateSurvivingAllyAtLeast(int count)
        => new BehaviorCondition(ConditionKind.SurvivingAllyAtLeast, 0f, count, 0);

    // 순수 판정. primitive만 소비
    public bool Evaluate(float selfHPRatio, int turnNum, int livingAllyCount)
    {
        switch (_kind)
        {
            case ConditionKind.Always:
                return true;
            case ConditionKind.SelfHPBelow:
                // 경계 규약 = 미만(<)으로 통일. "30% 이하 발동" 같은 요구는 threshold 값으로 조정(0.301 등 편법 금지)
                return selfHPRatio < _threshold;
            case ConditionKind.TurnNumberMod:
                return _intA > 0 && (turnNum % _intA) == _intB;   // 나눗수 0 방어(0 나눗셈 예외 차단)
            case ConditionKind.TurnNumberAtLeast:
                return turnNum >= _intA;
            case ConditionKind.SurvivingAllyAtLeast:
                return livingAllyCount >= _intA;
            default:
                return false;   // 미정의 kind = 매치 실패. 뒤의 Always 폴백이 잡음
        }
    }
}