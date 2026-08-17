// 9단계 판정 결과. 상위 전투 루프가 이 값으로 계속/종료 제어
// 웨이브 전환은 계속이라 Ongoing에 흡수 -> 승리/패배만 종료 시그널
public enum BattleOutcome
{ 
    Ongoing,    // 다음 턴 진행. 웨이브 전환 포함
    Victory,    // 마지막 웨이브 적 전멸
    Defeat      // 전 아군 전투불능
}