// 2021.02.22 편집
// 학습용으로 만들어졌으므로 사용할 수 없음

using System;
using System.Net.Sockets;
using System.Threading;

namespace Redwood
{
    // 클라이언트 상태 객체 수신 스레드를 안전하게 핸들하기 위함
    // => 새로운 연결마다 새로운 객체를 생성할 수 있음
    // => 완벽히 데이터 경쟁(충돌)을 예방함
    // => 여기서 깨끗한 클라이언트 상태를 만드는 것이 데이터 경쟁을 막는 가장 좋은 방법!
    class ClientConnectionState : ConnectionState
    {
        public Thread receiveThread;

        // 연결된 상태 세가지 조건이 맞아야 연결이 true
        public bool Connected => client != null &&
                                 client.Client != null &&
                                 client.Client.Connected;

        // TcpClient는 '연결' 상태를 확인할 수 없음, 매뉴얼대로 추적
        // -> 스레드의 활성화 여부와 연결 여부가 모두 참인 것으로 확인하는 것 충분하지 않음
        //    연결이 비활성화된 짧은 시간동안 스레드가 활성화돼있고 연결 변수가 참인 경우. 원치 않는 조건이 활성화되므로.
        // -> 스레드 안전한 bool 래퍼를 쓰기로 함. 스레드 기능이 모두 static(정적)으로 남겨져야 하므로
        // => 처음 여기서 Connect()가 호출된 시점부터 TcpClient.Connect()가 호출되기까지 Connecting은 true가 됨
        // => volatile로 지정하여 컴파일러는 이를 최적화에서 제외
        public volatile bool Connecting;

        // 수신 메시지를 위한 스레드 안전 파이프
        // 각 연결마다 새로운 상태를 생성해야하므로 클라이언트 내부 클래스에서 동작
        // 모든 연결을 하나의 수신 파이프로 둔 서버와는 다름
        public readonly ReceivePipe receivePipe;

        // 생성자. 클라이언트 연결을 위해 
        public ClientConnectionState(int MaxMessageSize) : base(new TcpClient(), MaxMessageSize)
        {
            // pooling 목적으로 최대 메시지 크기의 수신 파이프 생성
            receivePipe = new ReceivePipe(MaxMessageSize);
        }

        // 모든 상태를 없애버림
        public void Dispose()
        {
            // 클라이언트 닫기
            client.Close();

            // 연결 해제 뒤 바로 Connect() 호출을 보장하도록 스레드가 끝날때까지 대기
            // -> .Join()은 때때로 영원한 대기에 빠짐. 이미 해제되어 죽은 것에 연결을 시도하려고 하기 때문에
            receiveThread?.Interrupt();

            // 수신 스레드를 종료했기 때문에 정상적으로 초기화
            Connecting = false;

            // 송신 파이프 정리, 내부 요소를 남길 필요 없음
            sendPipe.Clear();

            // 중요! 수신 파이프는 정리해선 안됨
            // Tick()에서 연결 해제 메시지를 처리하는 중임!

            // 해당 클라이언트는 완전히 날려버림
            // 아무도 사용하지 않을 것이며 Connected 다시 바로 false 상태로 만드는 방법임
            client = null;
        }
    }

    // @Redwood.
    // 클라이언트는 송신은 연결 당 하나의 파이프가 존재하지만 수신은 모든 루프에서 하나의 파이프를 공유해서 사용
    public class Client : Common
    {
        // events to hook into
        // => OnData uses ArraySegment for allocation free receives later
        public Action OnConnected;
        public Action<ArraySegment<byte>> OnData;
        public Action OnDisconnected;


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

        // 모든 클라이언트 상태를 객체로 묶었고 수신 스레드로 전달됨
        // => 오래된 죽은 스레드들이 이전 객체를 사용하려는 데이터 경쟁을 막기 위해 각 연결마다 새롭게 생성됨
        ClientConnectionState state;

        public bool Connected => state != null && state.Connected;
        public bool Connecting => state != null && state.Connecting;

        // 파이프 수, 벤치마크 / 디버깅에 유용
        public int ReceivePipeCount => state != null ? state.receivePipe.TotalCount : 0;

        // 생성자 Common에서 설정한 값이 해당 생성자로 전달이 안됨..?
        public Client(int MaxMessageSize = 1024) : base(MaxMessageSize) { }

        // 스레드 기능
        // 상태 공유를 피하기 위해 STATIC으로!
        // => ClientState 객체 전달, 하나는 하나의 새로운 스레드를 생성해야함!
        // => 오래된 죽은 스레드는 여전히 살아있는 스레드의 안전한 상태를 해칠 우려가 있음
        static void ReceiveThreadFunction(ClientConnectionState state, string ip, int port, int MaxMessageSize, bool NoDelay, int SendTimeout, int ReceiveTimeout, int ReceiveQueueLimit)
        {
            Thread sendThread = null;

            // try/catch로 감싸기!
            try
            {
                // connect (blocking)
                state.client.Connect(ip, port);
                state.Connecting = false; // volatile

                // 소켓 옵션 설정 Connect()에서 생성된 후
                state.client.NoDelay = NoDelay;
                state.client.SendTimeout = SendTimeout;
                state.client.ReceiveTimeout = ReceiveTimeout;

                // 연결 뒤 전송 스레드 시작
                // 중요! 여러 스레드에 상태를 공유하지 마십시오!
                sendThread = new Thread(() => { ThreadFunctions.SendLoop(0, state.client, state.sendPipe, state.sendPending); });
                sendThread.IsBackground = true;
                sendThread.Start();

                // 수신 루프 동작, 수신 파이프는 모든 루프에서 공유됨
                ThreadFunctions.ReceiveLoop(0, state.client, MaxMessageSize, state.receivePipe, ReceiveQueueLimit);
            }
            catch(SocketException exception)
            {
                // IP는 맞지만 서버가 돌아가지 않을때 메시지 남김
                Log.Info($"Client Recv: failed to connect to ip={ip} port={port} reason={exception}");

                // 'Disconnected' 이벤트를 수신 파이프에 넣어줌으로 Connect에 실패했다는 것을 알림. otherwise they will never know
                state.receivePipe.Enqueue(0, EventType.Disconnected, default);
            }
            catch (ThreadInterruptedException)
            {
                // expected if Disconnect() aborts it
            }
            catch (ThreadAbortException)
            {
                // expected if Disconnect() aborts it
            }
            catch (ObjectDisposedException)
            {
                // expected if Disconnect() aborts it and disposed the client
                // while ReceiveThread is in a blocking Connect() call
            }
            catch (Exception exception)
            {
                // something went wrong. probably important.
                Log.Error($"Client Recv Exception: {exception}");
            }

            // 전송 스레드는 ManualResetEvent에 계속 대기하므로 연결 해제시 끝내도록 명확하게 하기
            // 그렇지 않으면 전송하고 실패했을 때야 연결이 끊겼음을 알게 됨
            sendThread?.Interrupt();

            // Connect에 실패했다면 스레드는 닫힐것
            // 무슨 일이든 Connecting 상태를 초기화
            state.Connecting = false;

            // 여기까지 왔다면 끝난 것. 미리 수신 루프를 정리해야 하지만
            // 만약 연결이 안된 상태라면 할 수 없을 것. 그러므로 client 깨끗하게-
            state.client?.Close();
        }

        public void Connect(string ip, int port)
        {
            if(Connecting || Connected)
            {
                Log.Warning("Telepathy Client can not create connection because an existing connection is connecting or connected");
                return;
            }

            // 새롭게 만들어서 덮어쓰기!
            state = new ClientConnectionState(MaxMessageSize);

            // Connect가 성공 혹은 실패까지 Connecting은 true로
            state.Connecting = true;

            // 완벽한 IPv4, IPv6 및 호스트 이름 확인을 지원하는 TcpClient를 생성
            // 이하는 생성자 별 특징
            //
            // * TcpClient(hostname, port): works but would connect (and block)
            //   already
            // * TcpClient(AddressFamily.InterNetworkV6): takes Ipv4 and IPv6
            //   addresses but only connects to IPv6 servers (e.g. Telepathy).
            //   does NOT connect to IPv4 servers (e.g. Mirror Booster), even
            //   with DualMode enabled.
            // * TcpClient(): creates IPv4 socket internally, which would force
            //   Connect() to only use IPv4 sockets.
            //
            // => 트릭은 Connect가 호스트 이름을 확인하고 필요에 따라 IPv4 또는 IPv6 소켓을 만들 수 있도록 
            //    내부 IPv4 소켓을 지우도록 (TcpClient 소스 참조).
            state.client.Client = null;

            // client.Connect(ip, port)는 blocking이므로 스레드에서 호출하고 즉시 반환하도록 람다식 사용
            state.receiveThread = new Thread(() =>
            {
                ReceiveThreadFunction(state, ip, port, MaxMessageSize, NoDelay, SendTimeout, ReceiveTimeout, ReceiveQueueLimit);
            });

            state.receiveThread.IsBackground = true;

            state.receiveThread.Start();
        }

        public void Disconnect()
        {
            if(Connecting || Connected)
            {
                state.Dispose();
            }
        }

        // 서버 소켓 연결을 사용하여 메시지 전송
        public bool Send(ArraySegment<byte> message)
        {
            if(Connected)
            {
                if(message.Count <= MaxMessageSize)
                {
                    if(state.sendPipe.Count < SendQueueLimit)
                    {
                        // 스레드 안전을 위해 전송 파이프 추가와 반환을 바로
                        // 여기서 전송했을 때 다른 쪽에서 렉 걸렸거나 disconnected됐다면 매우 긴 시간동안 blocking될 수 있음
                        state.sendPipe.Enqueue(message);
                        state.sendPending.Set();
                        return true;
                    }
                    // 전송 큐가 너무 큰 상태라면 disconnect
                    // -> 입력보다 네트워크로 인한 큐 메모릭 부하 방지
                    // -> 로드 밸런싱 처리를 위해 연결 해제는 훌륭한 방식
                    //
                    // 노트: 전송 스레드는 한번에 전송 큐를 즉시 처리한다해도 
                    //      여전히 오랫동안 전송 큐가 너무 커져버리는 sending blocks가 발생 가능.. 한계가 있음
                    else
                    {
                        Log.Warning($"Client.Send: sendPipe reached limit of {SendQueueLimit}. " +
                            $"This can happen if we call send faster than the network can process messages. " +
                            $"Disconnecting to avoid ever growing memory & latency.");
                        
                        state.client.Close();
                        return false;
                    }
                }
                Log.Error($"Client.Send: message too big:{message.Count}. Limit:{MaxMessageSize}");
                return false;
            }
            Log.Warning("Client.Send: not connected!");
            return false;
        }


        // tick: 각 연결에 'limit'만큼의 메시지들을 처리
        // => 네트워크 부하로 서버와 클라이언트가 교착상태와 오랜 기간 얼어붙는 것을 피하도록 제한 파라미터를 둠
        // => Mirror와 DOTSNET에선 어쨌든 프로세스 제한이 필요하며 여기서 하면 편함
        // => 처리할 남은 메시지의 양을 반환하므로 호출자가 필요한 만큼 호출할 수 있음, processLimit
        //
        // Tick() 여러 메시지를 처리하지만 만약 씬을 바꾸는 메시지가 들어왔을 때 즉시 멈출 방법이 필요
        // Mirror는 씬 변환 시 처리할 수 있는게 없음 (다른 것에도 유용할 수 있음)
        // => 전송에서 람다를 한 번만 할당하도록 
        public int Tick(int processLimit, Func<bool> checkEnabled = null)
        {
            if(state ==null)
            {
                return 0;
            }

            for (int i=0; i<processLimit;++i)
            {
                if(checkEnabled != null && !checkEnabled())
                {
                    break;
                }

                if(state.receivePipe.TryPeek(out int _, out EventType eventType, out ArraySegment<byte> message))
                {
                    switch(eventType)
                    {
                        case EventType.Connected:
                            OnConnected?.Invoke();
                            break;
                        case EventType.Data:
                            OnData?.Invoke(message);
                            break;
                        case EventType.Disconnected:
                            OnDisconnected?.Invoke();
                            break;
                    }

                    state.receivePipe.TryDequeue();
                }
                else
                {
                    break;
                }
            }

            return state.receivePipe.TotalCount;
        }
    }
}
