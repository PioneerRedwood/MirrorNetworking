// 2021.02.22 편집
// 학습용으로 만들어졌으므로 사용할 수 없음

using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Redwood
{
    // 추상 전송 계층 구성 요소
    // 전송 규칙
    // 모든 전송은 올바르게 작동하도록 미러의 양식에 맞게 해당 규칙을 따라야함

    // Monobehaviour가 비활성화되면 Transport는 콜백을 호출하지 않아야함
    // 콜백은 메인 스레드에서 호출되어야하며 LateUpdate에서 수행하는 것이 가장 좋음
    // 콜백은 ServerStop 또는 ClientDisconnect가 호출 된 후 호출 가능
    // ServerStop 또는 ClientDisconnect는 여러 번 호출 할 수 있습니다.
    // Available은 플랫폼을 확인해야하며 전송이 일부에서만 작동하는 경우 32 비트와 64 비트를 확인해야함
    // GetMaxPacketSize는 전송이 실행되지 않는 경우에도 크기를 반환해야함
    // Channels.DefaultReliable은 신뢰할 수 있어야함

    public abstract class Transport : MonoBehaviour
    {
        // Redwood 에서 사용하는 현재 전송입니다.
        public static Transport activeTransport;

        // 일부 전송은 모바일에서만 사용할 수 있습니다.
        // Webgl에서 대부분 작동하지 않습니다.
        // 사용 예 : return Application.platform == RuntimePlatform.WebGLPlayer
        // 이 전송이 현재 플랫폼에서 작동하는 경우 참
        public abstract bool Available();

        #region Client

        // 서버로의 클라이언트 생성이 성공임을 구독자들에게 알림
        public Action OnClientConnected = () => Debug.LogWarning("OnClientConnected called with no handler");

        // 서버로부터 데이터 수신 시 구독자들에게 알림
        // param: data(ArraySegment<byte>)와 channel(int)
        public Action<ArraySegment<byte>, int> OnClientDataReceived = (data, channel) => Debug.LogWarning("OnClientDataReceived called with no handler");

        // 서버 통신 과정 중 에러 발생 시 구독자들에게 알림
        // param: error(Exception) 
        public Action<Exception> OnClientError = (error) => Debug.LogWarning("OnClientError called with no handler");

        // 서버로부터 연결이 해제 시 구독자들에게 알림
        public Action OnClientDisconnected = () => Debug.LogWarning("OnClientDisconnceted called with no handler");

        // 현재 서버와 연결됐는지 확인
        public abstract bool ClientConnected();

        // 서버로의 연결 설정
        // param: address(string) 연결하려는 서버의 IP 주소거나 서버의 전체 주소 도메인 네임(FQDN)
        public abstract void ClientConnect(string address);

        // 서버로의 연결 설정
        // param: uri(Uri) 연결하려는 서버의 주소
        public virtual void ClientConnect(Uri uri)
        {
            ClientConnect(uri.Host);
        }

        // 서버로 데이터 전송
        // param: channelId(int) 사용하려는 채널, 기본은 0
        // 그러나 일부 전송은 채널을 통해 신뢰할 수 없는(Unreliable), 암호화, 압축 또는 기타 기능을 제공할 수 있음
        // param: segment(ArraySegment<byte>) 서버로 보낼 데이터, 반환 후 재사용되므로 직접 사용하거나 내부에서 복사하여 사용할 것. 할당 없는 전송을 허용!
        public abstract void ClientSend(int channelId, ArraySegment<byte> segment);

        // 서버로부터 클라이언트 연결 해제
        public abstract void ClientDisconnect();

        #endregion

        #region Server

        // 해당 서버의 주소를 탐색, 네트워크 검색에 유용
        // return: 서버에 도달할 수 있는 Uri
        public abstract Uri ServerUri();

        // 서버에 연결되면 구독자들에게 알림
        // param: connId(int)
        public Action<int> OnServerConnected = (connId) => Debug.LogWarning("OnServerConnected called with no handler");

        // 클라이언트로부터 해당 서버의 데이터 수신시 구독자들에게 알림
        // param: connId(int), data(ArraySegment<byte>, channel(int)
        public Action<int, ArraySegment<byte>, int> OnServerDataReceived = (connId, data, channel) => Debug.LogWarning("OnServerDataReceived called with no handler");

        // 클라이언트와 통신 과정에서 에러 발생 시 구독자들에게 알림
        // param: connId(int), error(Exception)
        public Action<int, Exception> OnServerError = (connId, error) => Debug.LogWarning("OnServerError called with no handler");

        // 서버로부터 클라이언트 연결 해제시 구독자들에게 알림
        // param: connId(int)
        public Action<int> OnServerDisconnected = (connId) => Debug.LogWarning("OnServerDisconnected called with no handler");

        // 서버가 작동 중인지 확인
        // 해당 전송이 클라이언트로부터의 연결이 가능하다면 참
        public abstract bool ServerActive();

        // 클라이언트로부터 접속 대기
        public abstract void ServerStart();

        // 클라이언트로부터 데이터 수신
        // param: connectionId(int) 데이터를 보낼 클라리언트 연결 아이디
        // param: channelId(int) 사용될 채널 아이디, 전송은 채널을 사용하여 신뢰할 수 없는(Unreliable), 암호화, 압축 등 기타 기능을 구현할 수 있음
        // param: data(ArraySegment<byte>)
        public abstract bool ServerSend(int connectionId, int channelId, ArraySegment<byte> segment);

        // 서버로부터 클라이언트 연결 해제, 사람들 쫓는데 유용
        // param: connectionId(int) 연결 해제할 클라이언트 아이디
        // return: 클라이언트가 쫓겨났다면 참
        public abstract bool ServerDisconnect(int connectionId);

        // 클라이언트 주소 가져오기
        // param: connectionId
        // return: address(string)
        public abstract string ServerGetClientAddress(int connectionId);

        // 클라이언트 접속 대기를 멈추고 모든 연결을 해제
        public abstract void ServerStop();

        #endregion

        // 해당 채널의 최대 패킷 크기.
        // Unreliable 전송일 경우 작은 패킷만 전달 가능하고 Reliable 전송에 단편화된 채널을 이용한 전송일 경우 보통 큰 패킷을 전달
        // 전송이 실행 중이 아니거나 Available()이 거짓인 경우에도 항상 값을 반환해야 함 
        // Fallback과 다중 전송이 런타임(실행 중)에 전송 가능한 보낼 수 있는 가장 작은 패킷 크기를 찾아야하기 때문
        // param: channelId(int) 0: Reliable 1: Unreliable
        public abstract int GetMaxPacketSize(int channelId = 0);

        // 해당 채널의 최대 일괄처리 크기. default로 GetMaxPacketSize 사용
        // kcp 전송과 같은 일부 전송에선 거대한 크기의 패킷을 이용하다보니 일괄처리 중 느려지게 됨
        // param: channelId(int)
        // return: 제공된 채널을 통해 일괄처리돼야하는 바이트 크기
        public virtual int GetMaxBatchSize(int channelId) => GetMaxPacketSize(channelId);

        // 클라이언트와 서버 양측 전송을 종료
        public abstract void Shutdown();

        // Update를 그대로 사용할 경우에 데이터 경쟁(충돌)이 발생해 느려지므로 Update()함수를 차단
#pragma warning disable UNT0001 // 빈 유니티 메시지
        public void Update() { }
#pragma warning restore UNT0001 // 빈 유니티 메시지

        // 창을 종료하거나 에디터에서 Stop 버튼을 누를경우 종료(Quit)
        // OnApplicationQuit()을 실행할 수 있도록 virtual 키워드 사용
        public virtual void OnApplicationQuit()
        {
            // 전송을 종료(스레드들을 모두 종료)
            // 유니티 에디터에서 Stop을 누르면 다음 Start까지 스레드가 살아있음
            // 전송이 스레드를 사용한다면 다음 Start 이후가 아니라 지금 종료해야함
            Shutdown();
        }
    }

    public class RedwoodTransport : Transport
    {
        // TCP 4 bytes header
        public const string Scheme = "tcp4";

        public ushort port = 9000;

        [Header("Common")]
        [Tooltip("NoDelay 활성화로 Nagle 알고리즘이 비활성화될 수 있습니다")]
        public bool NoDelay = true;

        [Tooltip("전송 초과 시간 (단위: 밀리초)")]
        public int SendTimeout = 5000;

        [Tooltip("수신 초과 시간 (단위: 밀리초)")]
        public int ReceiveTimeout = 5000;

        [Header("Server")]
        [Tooltip("최대 메시지 크기를 작게 하여 할당 공격을 피합니다. 그렇지 않으면 공격자는 헤더 크기가 2GB 이상인 가짜 패킷을 여러개 보내어 서버 측에서 이를 할당하느라 메모리를 모두 소진합니다.")]
        public int serverMaxMessageSize = 16 * 1024;

        [Tooltip("서버 측에서 처리하는 속도보다 메시지가 빠르게 입력될 경우 교착상태에 빠지는 것을 방지하기 위해 서버 측에 제한을 둡니다.")]
        public int serverMaxReceivesPerTick = 10000;

        [Tooltip("각 연결의 보류 메시지에 대한 서버 송신 큐 제한. 서버와 연결된 모두 느려지게 하는 것보다 느린 클라이언트를 쫓아내는게 과부하를 막을 수 있습니다.")]
        public int serverSendQueueLimitPerConnection = 10000;

        [Tooltip("각 연결의 보류 메시지에 대한 서버 수신 큐 제한. 서버와 연결된 모두 느려지게 하는 것보다 느린 클라이언트를 쫓아내는게 과부하를 막을 수 있습니다.")]
        public int serverReceiveQueueLimitPerConnection = 10000;

        [Header("Client")]
        [Tooltip("최대 메시지 크기를 작게 하여 할당 공격을 피합니다. 그렇지 않으면 공격 호스트는 헤더 크기가 2GB 이상인 가짜 패킷을 여러개 보내어 연결된 클라이언트 측에서 이를 할당하느라 메모리를 모두 소진합니다.")]
        public int clientMaxMessageSize = 16 * 1024;

        [Tooltip("클라이언트 측에서 처리하는 속도보다 메시지가 빠르게 입력될 경우 교착상태에 빠지는 것을 방지하기 위해 서버 측에 제한을 둡니다.")]
        public int clientMaxReceivesPerTick = 10000;

        [Tooltip("보류 메시지에 대한 클라이언트 송신 큐 제한. 지연 시간의 증가를 피하기 위해 연결 대기열이 한계에 도달하면 이를 해제합니다.")]
        public int clientSendQueueLimit = 10000;

        [Tooltip("보류 메시지에 대한 클라이언트 수신 큐 제한. 지연 시간의 증가를 피하기 위해 연결 대기열이 한계에 도달하면 이를 해제합니다.")]
        public int clientReceiveQueueLimit = 10000;

        Redwood.Client client;
        Redwood.Server server;

        // 씬 변경 시 바로 멈추기 위해 Tick()의 매개변수를 확인
        // 한번만 할당
        Func<bool> enabledCheck;

        private void Awake()
        {
            client = new Redwood.Client(clientMaxMessageSize);
            server = new Redwood.Server(serverMaxMessageSize);

            // client 설정
            client.OnConnected = () => OnClientConnected.Invoke();
            client.OnData = (segment) => OnClientDataReceived.Invoke(segment, 0);
            client.OnDisconnected = () => OnClientDisconnected.Invoke();

            client.NoDelay = NoDelay;
            client.SendTimeout = SendTimeout;
            client.ReceiveTimeout = ReceiveTimeout;
            client.SendQueueLimit = clientSendQueueLimit;
            client.ReceiveQueueLimit = clientReceiveQueueLimit;

            // server 설정
            server.OnConnected = (connectionId) => OnServerConnected.Invoke(connectionId);
            server.OnData = (connectionId, segment) => OnServerDataReceived.Invoke(connectionId, segment, 0);
            server.OnDisconnected = (connectionId) => OnServerDisconnected.Invoke(connectionId);

            server.NoDelay = NoDelay;
            server.SendTimeout = SendTimeout;
            server.ReceiveTimeout = ReceiveTimeout;
            server.SendQueueLimit = serverSendQueueLimitPerConnection;
            server.ReceiveQueueLimit = serverReceiveQueueLimitPerConnection;

            enabledCheck = () => enabled;

            Debug.Log("RedwoodTransport 활성화");
        }

        public override bool Available()
        {
            // C#에 내장된 TCP 소켓은 WebGL 제외하곤 다 작동함
            return Application.platform != RuntimePlatform.WebGLPlayer;
        }

        public override bool ClientConnected() => client.Connected;

        public override void ClientConnect(string address) => client.Connect(address, port);

        public override void ClientConnect(Uri uri)
        {
            if(uri.Scheme != Scheme)
            {
                throw new ArgumentException($"Invalid uri{uri}, use {Scheme}://host:port instead", nameof(uri));
            }

            int serverPort = uri.IsDefaultPort ? port : uri.Port;
            client.Connect(uri.Host, serverPort);
        }

        public override void ClientDisconnect() => client.Disconnect();

        public override void ClientSend(int channelId, ArraySegment<byte> segment) => client.Send(segment);


        public void LateUpdate()
        {
            if(!enabled)
            {
                return;
            }

            client.Tick(clientMaxReceivesPerTick, enabledCheck);

            server.Tick(serverMaxReceivesPerTick, enabledCheck);
        }

        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            builder.Port = port;
            return builder.Uri;
        }

        public override bool ServerActive() => server.Active;

        public override bool ServerDisconnect(int connectionId) => server.Disconnect(connectionId);

        public override string ServerGetClientAddress(int connectionId)
        {
            try
            {
                return server.GetClientAddress(connectionId);
            }
            catch(SocketException)
            {
                return "Unknown";
            }
        }

        public override bool ServerSend(int connectionId, int channelId, ArraySegment<byte> segment) => server.Send(connectionId, segment);

        public override void ServerStart() => server.Start(port);

        public override void ServerStop() => server.Stop();
        

        public override void Shutdown()
        {
            Debug.Log("RedwoodTransport Shutdown()");
            client.Disconnect();
            server.Stop();
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            return serverMaxMessageSize;
        }

        public override string ToString()
        {
            if(server.Active && server.listener != null)
            {
                return $"Redwood server port: {port}";
            }
            else if(client.Connecting || client.Connected)
            {
                return $"Redwood client port: {port}";
            }

            return "Redwood (inactive/disconnected)";
        }
    }
}
