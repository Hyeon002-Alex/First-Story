// 아군 결정의 동기 소스. 요청을 받아 실행할 명령을 즉시 반환
// 스크립트 응답자와 향후 AI 조작 아군이 구현
// 실 UI는 즉시 반환 불가라 이 인터페이스가 아닌 InputRequest 슬롯을 비동기로 채우는 별도 드라이버로 붙음
public interface IPlayerInputSource
{
    // 요청 1건에 대한 결정. 반환 명령은 드라이버가 request.SetResponse로 적용
    ActionCommand Resolve(InputRequest request);
}