// 2021.02.22 편집
// 학습용으로 만들어졌으므로 사용할 수 없음

// Server, Client 양쪽 모두 필요한 연결 상태 객체
// -> server는 다중 연결에 대한 추적이 필요
// -> client는 데이터 경쟁*(data races, 사라진 스레드가 여전히 연결 상태를 수정하는 등)이 
// 발생하지 않도록 새로운 연결마다 안전하게 연결을 생성해야 한다.
// 매번 새로운 상태를 생성할 경우엔 발생할 수 없다! (모든 불안정한 테스트 수정)
// 
// ... 게다가, 코드를 공유할 수 있다

using System.Net.Sockets;
using System.Threading;

namespace Redwood
{
    public class ConnectionState
    {
        public TcpClient client;

        // 메인 스레드로부터 전송 스레드로의 스레드 안전 파이프
        public readonly SendPipe sendPipe;

        // ManualResetEvent 전송 스레드를 깨우기 위해 Thread.Sleep 보다 나음
        // -> Set() 호출, 만약 모두 전송됐으면
        // -> Reset() 호출, 만약 재전송할 것이 있다면
        // -> WatiOne() 재설정이 호출되기 전까지 대기
        public ManualResetEvent sendPending = new ManualResetEvent(false);

        public ConnectionState(TcpClient client, int MaxMessageSize)
        {
            this.client = client;

            // pooling을 위해 최대 메시지 크기의 전송 파이프 생성
            sendPipe = new SendPipe(MaxMessageSize);
        }
    }
}

