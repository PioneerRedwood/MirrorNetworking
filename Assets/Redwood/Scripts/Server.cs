// 해당 클래스는 Mirror의 Telepathy Server를 토대로 제작됨
// Mirror의 Telepathy는 TCP 방식으로 MMORPG 같은 네트워크 규모를 위해 설계
// 자세한 내용은 https://mirror-networking.com/docs/Articles/Transports/Telepathy.html

using System;
using System.Net;
using System.Net.Sockets;

using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;


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
        protected ReceivePipe receivePipe;

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

        // 생성자
        public Server(int MaxMessageSize) : base(MaxMessageSize) { }

        // listener 스레드의 listen 함수
        // 노트: 최대 연결에 대한 매개 변수는 없음. High level API 쪽에서 처리해야함
        //      Transport는 '너무 많음'의 내용의 메시지를 보낼 수 없음
        void Listen(int port)
        {
            // 스레드와 마찬가지로 반드시 try/catch 문으로 묶어줘야 함
            // 예외는 조용히 처리됨
            try
            {
                // .Create를 통해 IPv4, IPv6 둘의 listener가 시작됨
                listener = TcpListener.Create(port);
                listener.Server.NoDelay = NoDelay;
                listener.Server.SendTimeout = SendTimeout;
                listener.Server.ReceiveTimeout = ReceiveTimeout;
                listener.Start();

                Log.Info("Server: listening port=" + port);

                // 새로운 clients 연결 수락
                while(true)
                {
                    // 새로운 clients 대기와 수락
                    // 노트: 'using'은 스레드가 시작된 후 필요함에도 불구하고 버려버리기 때문에 진행을 망칠 수 있음
                    // note: 'using' sucks here because it will try to dispose after thread was started but we still need it in the thread
                    TcpClient client = listener.AcceptTcpClient();

                    // 소켓 설정
                    client.NoDelay = NoDelay;
                    client.SendTimeout = SendTimeout;
                    client.ReceiveTimeout = ReceiveTimeout;

                    // 다음 연결 아이디 생성을 위해 (스레드 안전)
                    int connectionId = NextConnectionId();

                    // 바로 dict에 추가
                    ConnectionState connection = new ConnectionState(client, MaxMessageSize);
                    clients[connectionId] = connection;

                    // 각 client마다 전송 스레드 스폰
                    Thread sendThread = new Thread(() =>
                    {
                        // 조용한 스레드 예외 처리를 위해 try/catch로 감싸야 함
                        try
                        {
                            // 전송 루프
                            // 중요! 다수 스레드에 상태를 공유하지 마십시오!
                            ThreadFunctions.SendLoop(connectionId, client, connection.sendPipe, connection.sendPending);
                        }
                        catch (ThreadAbortException)
                        {
                            // 아무 로그도 남기지 않고 멈춤
                            // SendLoop에서도 동일하게 실행됨. 중단될 경우 이곳으로 다시 옴. 에러 보이지 말것
                        }
                        catch (Exception exception)
                        {
                            Log.Error("Server send thread exception: " + exception);
                        }
                    });
                    sendThread.IsBackground = true;
                    sendThread.Start();

                    // 각 client 별 수신 스레드 스폰
                    Thread receiveThread = new Thread(() =>
                    {
                        // 조용한 스레드 예외 처리를 위해 try/catch로 감싸야 함
                        try
                        {
                            // ReceiveLoop 실행
                            // receive pipe 는 모든 루프에서 공유됨
                            ThreadFunctions.ReceiveLoop(connectionId, client, MaxMessageSize, receivePipe, ReceiveQueueLimit);

                            // 중요: 스레드가 끝나도 client에서 제거하지 마시오
                            // 연결 해제 이벤트가 여전히 파이프에서 수행되기 때문에 이는 Tick() 내에서 수행돼야 함
                            // client를 즉시 제거하면 파이프에서 연결 해제 프로세스가 진행되지 않은 채 잃어버리게 됨

                            // 전송 스레드는 ManualResetEvent를 기다리고 있음
                            // 연결이 해제됐다면 확실하게 전송 스레드를 중단해야 함
                            // 그렇지 않으면 전송 스레드는 연결이 해제된 동안 실제 전송 데이터를 보낼 때 끝나게 됨
                            sendThread.Interrupt();
                        }
                        catch (Exception exception)
                        {
                            Log.Error($"Server client thread exception: {exception}");
                        }
                    });
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            }
            catch(ThreadAbortException exception)
            {
                // 유니티 에디터에서 중단 예외를 발생할 것
                // 다음 시작 버튼을 눌렀을 때. 하지만 괜찮음
                Log.Info($"Server thread aborted. That's okay.{exception}");
            }
            catch(SocketException exception)
            {
                // StopServer 호출할 때 해당 스레드를 중단함
                // SocketException 을 발생시키면서. 하지만 괜찮음.
                Log.Info($"Server Thread stopped. That's okay.{exception}");
            }
            catch (Exception exception)
            {
                // 뭔가 잘못됨. 아마 중요할 거임
                Log.Error($"Server Exception: {exception}");
            }
        }

        // 백그라운드 스레드에서 새로운 연결을 수락하도록 시작하고 각각 새로운 스레드를 생성
        public bool Start(int port)
        {
            if(Active)
            {
                return false;
            }

            // pooling 하기 위해 최대 메시지 크기의 수신 파이프 생성
            // => 매번 새로운 파이프 생성!
            //    만약 오래된 수신 스레드가 종료중이라면 여전히 오래된 파이프를 사용하고 있는 것임
            //    여기 새로운 시작에서 오래된 데이터의 위험을 수반하고 싶지 않음
            receivePipe = new ReceivePipe(MaxMessageSize);

            // 새로운 listener 스레드
            // 낮은 우선순위. 만약 메인 스레드가 너무 바쁘다면 더 많은 client 받는 것은 중요하지 않음
            Log.Info($"Server: Start port={port}");
            listenerThread = new Thread(() => { Listen(port); });
            listenerThread.IsBackground = true;
            listenerThread.Priority = ThreadPriority.BelowNormal;
            listenerThread.Start();
            return true;
        }

        public void Stop()
        {

        }

        public bool Send(int connectionId, ArraySegment<byte> message)
        {

            return false;
        }

        public string GetClientAddress(int connectionId)
        {

            return "";
        }

        public bool Disconnect(int connectionId)
        {

            return false;
        }

        public int Tick(int processLimit, Func<bool> checkEnabled = null)
        {

            return receivePipe.TotalCount;
        }
    }

}
