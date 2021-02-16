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

        // 
    }
}

// common code used by server and client
namespace Telepathy
{
    public abstract class Common
    {
        // IMPORTANT: DO NOT SHARE STATE ACROSS SEND/RECV LOOPS (DATA RACES)
        // (except receive pipe which is used for all threads)

        // NoDelay disables nagle algorithm. lowers CPU% and latency but
        // increases bandwidth
        public bool NoDelay = true;

        // Prevent allocation attacks. Each packet is prefixed with a length
        // header, so an attacker could send a fake packet with length=2GB,
        // causing the server to allocate 2GB and run out of memory quickly.
        // -> simply increase max packet size if you want to send around bigger
        //    files!
        // -> 16KB per message should be more than enough.
        public readonly int MaxMessageSize;

        // Send would stall forever if the network is cut off during a send, so
        // we need a timeout (in milliseconds)
        public int SendTimeout = 5000;

        // Default TCP receive time out can be huge (minutes).
        // That's way too much for games, let's make it configurable.
        // we need a timeout (in milliseconds)
        public int ReceiveTimeout = 5000;

        // constructor
        protected Common(int MaxMessageSize)
        {
            this.MaxMessageSize = MaxMessageSize;
        }
    }
}
