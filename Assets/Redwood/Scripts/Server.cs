// 해당 클래스는 Mirror의 Telepathy Server를 토대로 제작됨
// Mirror의 Telepathy는 TCP 방식으로 MMORPG 같은 네트워크 규모를 위해 설계
// 자세한 내용은 https://mirror-networking.com/docs/Articles/Transports/Telepathy.html

using System;
using System.Net;
using System.Net.Sockets;

using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;


// 추가 학습 요구되는 C# 확장 지식
// delegate:    Action<T>, Func<T>
// Threading:   Threading.Thread / Interlocked
// 

namespace Redwood
{
    public class Server : Common
    {
        // 들어올 때 이벤트 발생
        public Action<int> OnConnected;
        public Action<int, ArraySegment<byte>> OnData;
        public Action<int> OnDisconnected;

        // 리스너
        public TcpListener listener;
        Thread listenerThread;

        // 만약 전송 큐가 너무 커지면 연결 해제
        // -> 입력보다 네트워크가 느려지는 것을 방지하기 위해
        // -> 로드 밸런싱보다 연결 해제가 더 나음
        //    서버 입장에서 전체 연결에 영향을 끼치는 것보다 하나를 끊는 것이 나음
        // -> 막대한 크기의 큐는 다수의 초동안 지연을 발생시킴 (latency)

        // Mirror 최대 메시지 크기를 16kb로 설정
        // limit =  1,000 연결당  16 MB 메모리를 의미
        // limit = 10,000 연결당 160 MB 메모리를 의미
        public int SendQueueLimit = 10000;
        public int ReceiveQueueLimit = 10000;

        // 메시지 수신을 위한 스레드 안전 파이프
        // 중요: 불행히도 하나의 연결에 하나의 파이프는 
        // 150 CCU 테스트시 다소 느려짐 모든 연결에 하나의 파이프를 두는것이 아름다움
        protected Utils.ReceivePipe receivePipe;

        // 파이프 개수, 디버깅과 벤치마크 시 유용
        public int ReceivePipeTotalCount => receivePipe.TotalCount;

        // Client <connectionId, ConnectionState>
        readonly ConcurrentDictionary<int, ConnectionState> clients = new ConcurrentDictionary<int, ConnectionState>();

        // 연결 아이디 개수
        int counter;

        // public 다음 id 함수. 누군가 id를 요구할 경우
        // 호스트모드라면 0, 외부 연결이면 1로 시작
        public int NextConnectionId()
        {
            int id = Interlocked.Increment(ref counter);

            // 연결이 20억 제한에 도달하면 연결 하나에 1초 걸린다해도 모두 처리하려면 68년이 걸림
            // -> 하지만 이런 일이 발생하면 caller가 clients의 수신을 허용하지 않도록 예외를 처리해야 함
            // -> 그러한 덕분에 bool Next(out id) 사용할 가치는 없다
            if(id == int.MaxValue)
            {
                throw new Exception("connection id limit reached: " + id);
            }

            return id;
        }

        // 서버가 돌아가고 있는 체크
        public bool Active => listenerThread != null && listenerThread.IsAlive;

        // 생성자 -- 여기부터 다시
        //public Server(int MaxMessageSize) : base(MaxMessageSize) { }
    }

}
