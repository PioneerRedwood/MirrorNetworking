// 중요!
// 스레드 기능은 반드시 static으로 할 것
// => Common.Send/ReceiveLoop 는 스레드 간에 상태를 공유하는게 쉽기 때문에 매우 위험함
// => header, buffer, payload 등 이들은 static에서 non static으로 바뀐 뒤 예기치 않게 공유됨
// => C#에선 자동적으로 데이터 경쟁을 감지 못하므로 
//    최선의 방법은 스레드 모두 static 함수로 이동하며 그 안에서 상태를 체크해야함
//
// 100% static class로 유지해야하므로 non static 함수로 바꿔선 안됨!
using System;
using System.Net.Sockets;
using System.Threading;

namespace Redwood
{
    public static class ThreadFunctions
    {
        // stream을 경유하는 전송 메시지 <size, content> 구조
        // 해당 함수는 가끔 blocking을 일으킴
        // 가령, 지연 시간이 높거나 연결이 해제될 경우
        // -> payload는 여러 [<size, content>, <size, content>, ...] 부분으로 구성
        public static bool SendMessagesBlocking(NetworkStream stream, byte[] payload, int packetSize)
        {
            // 만약 client가 빠른 주기로 전송하거나 server가 멈추면 stream.Write 에서 예외를 던짐
            try
            {
                // 모든 것 쓰기
                stream.Write(payload, 0, packetSize);
                return true;
            }
            catch (Exception exception)
            {
                // server가 가끔 종료되기 때문에 정기적인 메시지를 로그로 남김
                Log.Info("Send: stream.Write exception: " + exception);
                return false;
            }
        }

        // stream을 경유하는 메시지 읽기 blocking
        // 할당을 피하기 위해 byte[]을 쓰고 반환함
        public static bool ReadMessageBlocking(NetworkStream stream, int MaxMessageSize, byte[] headerBuffer, byte[] payloadBuffer, out int size)
        {
            size = 0;

            // 버퍼는 (Header + 최대 메시지 크기)만큼 필요
            if (payloadBuffer.Length != 4 + MaxMessageSize)
            {
                Log.Error($"ReadMessageBlocking: payloadBuffer needs to be of size 4 + MaxMessageSize = {4 + MaxMessageSize} instead of {payloadBuffer.Length}");
                return false;
            }

            // 정확히 4 bytes 만큼 읽어야 함 (blocking)
            if (!stream.ReadExactly(headerBuffer, 4))
            {
                return false;
            }

            // int로 변환
            // Telepathy에선 Utils라는 개별 클래스를 만들어 함수로 불러옴
            // 비트 연산자를 써본 경험이 없어서 어떤 방식으로 수정되는지 생각해봐야..
            size = (headerBuffer[0] << 24) | (headerBuffer[1] << 16) | (headerBuffer[2] << 8) | headerBuffer[3];

            // 할당 공격을 방지해야함
            // 공격자는 한 행당 2GB 크기의 header 여러 패킷을 전송하면, 서버가 이를 처리하기 위해 각 패킷에 2GB를 할당하다 메모리가 폭발함
            //
            // 또한 크기가 0 이하인 경우 문제가 되므로
            if (size > 0 && size <= MaxMessageSize)
            {
                // 'size' bytes 크기의 내용만큼만 읽어오기 (물론 blocking)
                return stream.ReadExactly(payloadBuffer, size);
            }
            Log.Warning("ReadMessageBlocking: possible header attack with a header of: " + size + " bytes.");
            return false;
        }

        // client나 server의 client 모두 동일한 수신 스레드 함수
        public static void ReceiveLoop(int connectionId, TcpClient client, int MaxMessageSize, ReceivePipe receivePipe, int QueueLimit)
        {
            // client의 NetworkStream 가져오기
            NetworkStream stream = client.GetStream();

            // 매 수신 루프마다 런타임 시 할당을 피하기 위해 HeaderSize + 최대 메시지 크기만큼의 수신 버퍼가 있어야 함
            //
            // 중요! 해당 멤버는 사용하면 안됨. 그렇지 않으면 server 측에선 매 연결 마다 동시에 같은 buffer를 사용하게 됨
            byte[] receiveBuffer = new byte[4 + MaxMessageSize];

            // 새로운 header[4] 할당을 피하도록!
            //
            // 중요! 해당 멤버는 사용하면 안됨. 그렇지 않으면 server 측에선 매 연결 마다 동시에 같은 buffer를 사용하게 됨
            byte[] headerBuffer = new byte[4];

            // 조용하게 예외 처리를 하기 위해 당연하게도 try/catch로 묶어놔야 함
            try
            {
                // 파이프에 새로운 연결 이벤트 추가
                receivePipe.Enqueue(connectionId, EventType.Connected, default);

                // 읽은 데이터에 대해서
                // -> 일반적으로 수신하는 동안 가능한 한 많이 읽고 <size, content>, <size, cotent> 형태로 추출함
                //    이는 매우 복잡하며 비싼 행동임은 틀림없음
                // -> 여기서 trick을 사용:
                //    Read(2) -> size
                //      Read(size) -> content
                //    repeat
                //    읽기는 blocking이지만 모든 메시지가 도착하기까지 기다리는 것이 최선이기 때문에 이는 상관없음.
                // => 이는 가장 아름다우면서도 빠른 해결책.
                //    + no resizing 크기 재설정도 없고,
                //    + no extra allocations, just one for the content 내용에 대해 한번만 있을 뿐 추가적인 할당도 없고,
                //    + no crazy extracion logic 추출하는 작업도 없음
                while (true)
                {
                    // 다음 메시지 읽기 (blocking) 혹은 stream이 닫혀있으면 멈춤
                    if (!ReadMessageBlocking(stream, MaxMessageSize, headerBuffer, receiveBuffer, out int size))
                        // 반환대신 중단하기 때문에 stream 닫기가 계속 발생!
                        break;

                    // 메시지 읽기를 위해 ArraySegment 생성
                    ArraySegment<byte> message = new ArraySegment<byte>(receiveBuffer, 0, size);

                    // 파이프를 경유하여 메인 스레드에 전송
                    // -> 이 메시지를 내부적으로 복사하기 때문에 다음에 수신 버퍼를 재활용할 수 있음
                    receivePipe.Enqueue(connectionId, EventType.Data, message);

                    // 파이프가 해당 연결 아이디에 대해 너무 커진 경우 연결 해제
                    // -> 큐 메모리가 계속 커지며 입력보다 네트워크가 느려질 수 있는 상황을 피하기 위해
                    // -> 연결 해제는 load balancing을 막기위해 훌륭한 방법
                    //    서버 전체 연결에 위험을 안기기보다 하나를 해제하는 것이 나음
                    if (receivePipe.Count(connectionId) >= QueueLimit)
                    {
                        // log the reason 사유 로그로 남기기
                        Log.Warning($"receivePipe reached limit of {QueueLimit} for connectionId {connectionId}. " +
                            $"This can happen if network messages come in way faster than we manage to process them. Disconnecting this connection for load balancing.");

                        // 중요! 전체 큐를 정리하지 마십시오. 전체 연결에 하나의 큐를 사용하기 때문에
                        //receivePipe.Clear(); -> 위의 이유로 주석처리 됨

                        // 그저 중단. finally{}에서 모두 닫을 예정
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                // 뭔가 잘못됨. 스레드가 방해를 받았거나 연결이 해제됐거나 연결을 우리가 해제했거나
                // -> 모두 정상적으로 종료되도록 해야 함
                Log.Info($"ReceiveLoop: finished receive function for connectionId={connectionId} reason: {exception}");
            }
            finally
            {
                // 어떠한 경우든 닫기
                stream.Close();
                client.Close();

                // 적절히 연결 해제 후 'Disconnected' 메시지 추가
                // -> 상태 충돌을 피하기 위해 항상 닫은 후 수행해야 함
                // -> 연결 해제 중에 재연결 시도하면 연결을 닫기 전 짧은 순간에는 여전히 연결 상태가 참이므로 수행되지 않음
                receivePipe.Enqueue(connectionId, EventType.Disconnected, default);
            }
        }

        // 스레드 전송 함수
        // 노트: 연결 당 하나의 스레드가 필요함, 연결이 막힌 상태에도 나머지에서 수신을 지속해야하기 때문에
        public static void SendLoop(int connectionId, TcpClient client, SendPipe sendPipe, ManualResetEvent sendPending)
        {
            // client로부터 NetworkStream 얻어오기
            NetworkStream stream = client.GetStream();

            // payload[packetSize] 할당을 피해야. 동적으로 일괄처리하기 위한 크기가 증가하므로
            // 
            // 중요! 해당 멤버는 사용하면 안됨. 그렇지 않으면 server 측에선 매 연결 마다 동시에 같은 buffer를 사용하게 됨
            byte[] payload = null;

            try
            {
                while (client.Connected) // 마지막에는 닫히게 되므로 이렇게 시도
                {
                    // 어떠한 것도 하기 전에 ManualResetEvent를 초기화하면 충돌 혹은 경쟁 상태를 일으키지 않음
                    // 여기에 있는 동안 Send() 재호출 시 다음에 올바르게 감지됨
                    // -> otherwise Send might be called right after dequeue but
                    //    before .Reset, which would completely ignore it until
                    //    the next Send call.
                    // -> 그렇지 않으면 Send가 dequeue 직후에 호출될 수 있지만 .Reset 이전에 호출될 수 있으며 다음 Send 호출까지 완전히 무시됨
                    sendPending.Reset(); // WaitOne() .Set() 다시 호출되기까지 block

                    // dequeue & serialize all 큐에서 꺼내고 모두 직렬화
                    // removed SafeQueue.cs 2021-02-04
                    if (sendPipe.DequeueAndSerializeAll(ref payload, out int packetSize))
                    {
                        // 메시지 전송 (blocking) 혹은 stream이 닫힌 경우 멈춤
                        if (!SendMessagesBlocking(stream, payload, packetSize))
                            // 반환대신 중단하기 때문에 stream 닫기가 계속 발생!
                            break;
                    }

                    // CPU를 질식시키지 말고 큐가 빌 때까지 기다려라
                    sendPending.WaitOne();
                }
            }
            catch(ThreadAbortException)
            {
                // 발생 시 멈춤. 아무 로그 없음
            }
            catch(ThreadInterruptedException)
            {
                // 수신 스레드가 전송 스레드에 의해 방해될 때 멈춤
            }
            catch(Exception exception)
            {
                // 뭔가 잘못됨. 스레드가 방해를 받았거나 연결이 해제됐거나 연결을 우리가 해제했거나
                // -> 모두 정상적으로 종료되도록 해야 함
                Log.Info($"SendLoop Exception connectionId={connectionId} reason: {exception}");
            }
            finally
            {
                // 어떠한 경우든 닫기
                // 'host has failed to respond'와 같은 경우 전송 시 SocketException이 발생할 것이며
                // 이때 ReceiveLoop에 대해 연결 해제하고 연결 해제에 대한 메시지를 통보해야 함
                // 그렇지 않으면 아무것도 보낼 수 없는 연결이 영원히 살아있게 됨
                stream.Close();
                client.Close();
            }
        }
    }

}
