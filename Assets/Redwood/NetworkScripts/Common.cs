// 2021.02.22 편집
// 학습용으로 만들어졌으므로 사용할 수 없음

// Server와 Client 공통으로 사용되는 부분
namespace Redwood
{
    public abstract class Common
    {
        // 중요! 송수신 루프 사이의 상태를 공유하지 말것
        // 모든 스레드에서 사용되는 수신 파이프는 제외

        // NoDelay는 nagle 알고리즘을 사용 안함으로 cpu%와 지연시간을 줄이고 대역폭 증가시킴
        public bool NoDelay = true;

        // 메모리 할당 공격을 예방(allocation attacks). 각 패킷은 헤더 길이로 고정
        // 공격자는 크기가 2GB인 가짜 패킷을 전송할 수 있음
        // 이는 서버 메모리 할당을 2GB를 처리하는 동시에 메모리 부하가 발생함
        // -> 더 큰 파일을 보내고 싶다면 단순히 최대 패킷 크기를 증가시켜라!
        // -> 메시지당 16KB 크기면 충분할 것
        public readonly int MaxMessageSize;

        // 만약 네트워크가 전송 중 연결이 끊기게 되면 전송이 영원히 멈추므로 시간 제한을 둔다
        public int SendTimeout = 5000;

        // 기본 TCP 전송 시간 제한은 매우 커질 수 있음 (분 단위)
        // 게임에겐 너무나 버거운 시간임 설정 가능하도록 변경
        // 밀리초 단위로 바꿀 필요가 있음
        public int ReceiveTimeout = 5000;

        // 생성자
        protected Common(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
        }
    }
}