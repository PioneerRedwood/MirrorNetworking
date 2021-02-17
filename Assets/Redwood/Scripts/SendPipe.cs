// 삶의 복잡성으로부터 보호하기 위한 전송 파이프
// 메인 스레드에서 전송 스레드로 안전한 메시지 전송
// -> 스레드 안전성 내장
// -> byte[] 풀링 예정
//
// => telepathy로부터 모든 복잡성을 감춤
// => 스택/큐/동시성큐 등으로 변환하기 용이
// => 테스트하기에 용이

using System;
using System.Collections.Generic;

namespace Redwood
{
    public class SendPipe
    {
        // 메시지 큐
        // 동시성 큐 할당 lock{} 대신
        // -> byte 배열은 항상 최대 메시지 크기임
        // -> ArraySegment는 실제 메시지 내용이 담김
        //
        // 중요! lock{} 항상 사용됨!
        readonly Queue<ArraySegment<byte>> queue = new Queue<ArraySegment<byte>>();

        // byte[] 풀 사용, 할당 피하기 위해
        // "가져오기"와 "반환하기"(Take & return)는 파이프를 캡슐화하기에 아름다운 구조
        // 외부에선 아무런 걱정할 필요 없으며 테스트하기에도 용이함
        Pool<byte[]> pool;

        // 생성자
        public SendPipe(int MaxMessageSize)
        {
            pool = new Pool<byte[]>(() => new byte[MaxMessageSize]);
        }

        // for statistics. don't call Count and assume that it's the same after the call.
        public int Count
        {
            get { lock (this) { return queue.Count; } }
        }

        // 테스트를 위한 pool 개수
        public int PoolCount
        {
            get { lock (this) { return pool.Count(); } }
        }

        // 메시지 넣기 Enqueue a message
        // 전송 후 할당 해제를 위해 ArraySegment
        // -> segment의 배열은 반환시까지만 사용됨
        public void Enqueue(ArraySegment<byte> message)
        {
            // pool & queue 사용 시에 Lock!
            lock(this)
            {
                // ArraySegment는 반환될 때까지만 유효
                // byte[]에 복사해야 저장이 가능, 큐가 안전하게 유지되기 위해선 복사한 뒤 byte[]에 넣어줌

                // 할당을 피하기 위해 풀에서 하나 가져옴
                byte[] bytes = pool.Take();

                // 복사하고
                // System.Buffer.BlockCopy(Array src, int srcOffset, Array dst, int dstOffset, int count)
                Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);

                // 어느 부분이 메시지인지 지정
                ArraySegment<byte> segment = new ArraySegment<byte>(bytes, 0, message.Count);

                // 메시지 넣기
                queue.Enqueue(segment);
            }
        }

        // 전송 스레드는 각 byte[] 배열에 꺼내고 소켓에 써야 함
        // -> 다른 작업 후 하나의 byte[]을 큐에서 꺼내지만 전체를 바로 꺼내는 것보다 느림!
        //    lock{}과 DequeueAll이 ConcurrentQueue와 Dequeue 보다 빠름:
        // 
        //      uMMORPG 450 CCU(ConCurrent User, 동시 접속자 수)
        //        SafeQueue:        900 - 1440ms 지연 시간
        //        ConcurrentQueue:        2000ms 지연 시간
        // 
        // -> 가장 확실한 해결책은 byte[]에 관한 리스트를 (할당)반환한 뒤 소켓에 쓰는 것!
        // -> 더 빠른 해결책은 각각을 하나의 payload buffer에 직렬화한 뒤
        //    그것을 소켓에 단 한번만 넘겨주는 것. 더 적은 소켓 호출은 CPU 성능을 향상하는 방법(!)
        // -> 각 Entry에 대한 리스트 할당을 피하기 위해 단순하게 모든 Entry에 대해 payload에 미리 직렬화하도록 한다
        // => 파이프에 내장된 모든 복잡도는 테스트와 수정을 매우 편리하게 함!
        //
        // 중요! 이곳에서 직렬화하면 완전히 할당을 피하기 위해 후에 byte[] 항목를 다시 pool로 반환할 수 있다
        public bool DequeueAndSerializeAll(ref byte[] payload, out int packetSize)
        {
            // pool & queue 사용 시에 lock!
            lock(this)
            {
                // 비어있다면 아무것도 하지 않음
                packetSize = 0;
                if(queue.Count == 0)
                {
                    return false;
                }

                // 보류중인 메시지가 있을 수 있음
                // TCP 오버헤드를 피하고 성능을 향상하기 위해 하나의 패킷으로 병합
                //
                // 중요! Mirror & DOTSNET 에선 최대 메시지 크기로 일괄처리되지만
                //      보류중인 메시지를 하나의 payload로 포장하고 TCP에 넘긴다
                //      성능면에서 이득을 보기 때문에 유지한다!
                packetSize = 0;
                foreach(ArraySegment<byte> message in queue)
                {
                    // header + content
                    packetSize += 4 + message.Count;
                }

                // payload buffer가 생성되지 않았거나 전에 만든것이 너무 작았다면 생성
                // 중요! payload.Length는 packetSize보다 작을 수 있음 사용해서는 안됨!
                if(payload == null || payload.Length < packetSize)
                {
                    payload = new byte[packetSize];
                }

                // 모든 byte[] dequeue 패킷 내로 직렬화
                int position = 0;
                while(queue.Count > 0)
                {
                    // dequeue
                    ArraySegment<byte> message = queue.Dequeue();

                    // buffer 해당 위치에 header(size) 쓰기

                    // write header (size) into buffer at position
                    // Telepathy에선 Utils라는 개별 클래스를 만들어 함수로 불러옴
                    // 비트 연산자를 써본 경험이 없어서 어떤 방식으로 수정되는지 생각해봐야..
                    payload[position + 0] = (byte)(message.Count >> 24);
                    payload[position + 1] = (byte)(message.Count >> 16);
                    payload[position + 2] = (byte)(message.Count >> 8);
                    payload[position + 3] = (byte)message.Count;
                    position += 4;

                    // position에 payload 메시지 복사 
                    Buffer.BlockCopy(message.Array, message.Offset, payload, position, message.Count);
                    position += message.Count;

                    // 재사용(할당하지 않는!)을 위해 pool에 반환
                    pool.Return(message.Array);
                }

                // 직렬화 했으니
                return true;
            }
        }

        public void Clear()
        {
            // pool & queue 사용 시에 lock!
            lock(this)
            {
                // byte[]을 pool에서 반환하기 위해 dequeue하면서 정리
                while(queue.Count > 0)
                {
                    pool.Return(queue.Dequeue().Array);
                }
            }
        }
    }
}
