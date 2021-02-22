// 2021.02.22 편집
// 학습용으로 만들어졌으므로 사용할 수 없음

using System;
using System.Collections.Generic;

namespace Redwood
{
    // 수신 스레드와 메인 스레드 충돌을 방지하기 위한 파이프
    public class ReceivePipe
    {
        // 큐 엔트리 메시지 구조체는 여기에서만 쓰임
        // -> byte 배열은 항상 4 + 최대 메시지 크기
        // -> ArraySegment은 실제 메시지 내용을 담고 있음
        struct Entry
        {
            public int connectionId;
            public EventType eventType;
            public ArraySegment<byte> data;
            public Entry(int connectionId, EventType eventType, ArraySegment<byte> data)
            {
                this.connectionId = connectionId;
                this.eventType = eventType;
                this.data = data;
            }
        }

        // 메시지 큐
        // ConcurrentQueue 할당 lock{} 문구 대신
        // 중요: lock{} 항상 사용하기!
        readonly Queue<Entry> queue = new Queue<Entry>();

        // byte[] 풀 사용, 할당 피하기 위해
        // "가져오기"와 "반환하기"(Take & return)는 파이프를 캡슐화하기에 아름다운 구조
        // 외부에선 아무런 걱정할 필요 없으며 테스트하기에도 용이함
        Pool<byte[]> pool;

        // 불행히도 하나의 파이프에 하나의 연결 아이디 설정은 CCU 테스트에서 느린 방법임
        // 지금은 모든 연결에 하나의 파이프를 두기로 함
        // => 여전히 연결당 제한된 큐 메시지가 필요함. 
        //    이는 연결 내 메시지들이 큐를 가득차게 하여 모두를 느리게 하는 것을 방지하기 위해
        // => 지금 당장은 간단한 아이디별 카운터를 두기로
        Dictionary<int, int> queueCounter = new Dictionary<int, int>();

        // 생성자
        public ReceivePipe(int MaxMessageSize)
        {
            // byte[]를 최대 메시지 크기만큼 매번 초기화
            pool = new Pool<byte[]>(() => new byte[MaxMessageSize]);
        }

        // 연결 아이디에 맞는 큐 메시지의 크기를 반환
        // for statistics. don't call Count and assume that it's the same after the call.
        public int Count(int connectionId)
        {
            lock (this)
            {
                return queueCounter.TryGetValue(connectionId, out int count)
                    ? count
                    : 0;
            }
        }

        // 전체 크기
        public int TotalCount
        {
            get { lock (this) { return queue.Count; } }
        }

        // 풀 카운트. 테스트 목적
        public int PoolCount
        {
            get { lock (this) { return pool.Count(); } }
        }

        // 큐에서 메시지 꺼내기 
        // -> 후에 할당을 피하기 위해 ArraySegment 사용
        // -> 매개 변수가 직접 전달돼 큐에 넣고 ArraySegment 내부 byte[]에 저장
        public void Enqueue(int connectionId, EventType eventType, ArraySegment<byte> message)
        {
            // 풀과 큐를 사용시엔 항상 lock을 걸어야함
            lock (this)
            {
                // 해당 메시지가 배열 형태라면?
                ArraySegment<byte> segment = default;
                if (message != default)
                {
                    // ArraySegment는 반환될 때까지만 유효
                    // byte[]에 복사해야 저장이 가능, 큐가 안전하게 유지되기 위해선 복사한 뒤 byte[]에 넣어줌

                    // 할당을 피하기 위해 풀에서 하나 가져옴
                    byte[] bytes = pool.Take();

                    // 복사하고
                    // System.Buffer.BlockCopy(Array src, int srcOffset, Array dst, int dstOffset, int count)
                    Buffer.BlockCopy(message.Array, message.Offset, bytes, 0, message.Count);

                    // 어느 부분이 메시지인지 지정
                    segment = new ArraySegment<byte>(bytes, 0, message.Count);
                }

                // 빼기
                Entry entry = new Entry(connectionId, eventType, segment);
                queue.Enqueue(entry);

                // 연결 아이디에 맞게 카운터 증가시키기
                int oldCount = Count(connectionId);
                queueCounter[connectionId] = oldCount + 1;
            }
        }

        // 새로운 메시지 엿보기(?) peek
        // -> 파이프가 byte[] 작업 수행중일 때도 해당 프로세스의 호출을 허용
        // -> 풀에서 byte[]가 반환돼야하므로 큐에서 꺼내는 것(TryDequeue)이 이 작업 다음으로 수행돼야 한다
        // TryDequeue의 코멘트를 살필 것!
        // 중요! TryPeek과 Dequeue는 같은 스레드에서 진행돼야 함
        public bool TryPeek(out int connectionId, out EventType eventType, out ArraySegment<byte> data)
        {
            connectionId = 0;
            eventType = EventType.Disconnected;
            data = default;

            // pool과 queue 작업은 항상 lock 잠김으로!
            lock (this)
            {
                if (queue.Count > 0)
                {
                    Entry entry = queue.Peek();
                    connectionId = entry.connectionId;
                    eventType = entry.eventType;
                    data = entry.data;
                    return true;
                }
                return false;
            }
        }

        // 다음 메시지 꺼내기 Dequeue
        // -> 풀에 있다면 단순히 메시지 꺼내고 byte[] 반환
        // -> 만약 파이프가 여전히 byte[] 내부 수행 중이라면 첫째 요소를 실제 수행하기 위해 Peek을 사용
        // -> 풀 내부에 있는 byte[]이 반환돼야 하므로 요소를 반환할 필요는 없음
        //    풀 내부에서 반환된 byte[]를 다시 반환하는 것을 허락하지 않음
        // => Peek과 Dequeue는 가장 단순! 수신 파이프 풀링(receive pipe polling)을 깨끗하게 정리할 것. 할당하지 않도록
        // 중요! TryPeek과 Dequeue는 같은 스레드에서 진행돼야 함
        public bool TryDequeue()
        {
            // pool과 queue 작업은 항상 lock 잠김!
            lock (this)
            {
                if (queue.Count > 0)
                {
                    // queue에서 dequeue
                    Entry entry = queue.Dequeue();

                    // 만약 pool에 있다면 byte[] 반환
                    // 모든 메시지가 byte[]을 갖고 있지 않음
                    if (entry.data != default)
                    {
                        pool.Return(entry.data.Array);
                    }

                    // 연결 아이디에 맞는 카운터 감소
                    queueCounter[entry.connectionId]--;

                    // 0이면 제거
                    // 오래된 연결 아이디가 영원히 남아있는 것을 원치 않음. 천천히 메모리를 갉아먹는 문제 발생 가능
                    if (queueCounter[entry.connectionId] == 0)
                    {
                        queueCounter.Remove(entry.connectionId);
                    }

                    return true;
                }
                return false;
            }
        }

        public void Clear()
        {
            // pool과 queue는 작업 시 항상 Lock!
            lock (this)
            {
                // queue 정리. 하지만 pool 내 byte[]는 모두 꺼내야함!
                while (queue.Count > 0)
                {
                    // dequeue
                    Entry entry = queue.Dequeue();

                    // pool 내 byte[] 존재 시 반환
                    // 모든 메시지가 byte[]을 갖고 있지 않음
                    if (entry.data != default)
                    {
                        pool.Return(entry.data.Array);
                    }
                }

                // 카운터 또한 정리
                queueCounter.Clear();
            }
        }
    }
}
