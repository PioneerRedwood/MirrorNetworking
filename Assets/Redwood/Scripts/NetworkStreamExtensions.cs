// NetworkSteramExtensions
// 배경 지식: C#은 매개변수에 this를 던져줌으로 상속을 하지 않고도 클래스 내 override된 멤버 함수를 재정의하여 자유로이 확장할 수 있음
// 각 함수들의 주석은 왜 확장됐는지 설명함

using System.IO;
using System.Net.Sockets;

namespace Redwood
{
    public static class NetworkStreamExtensions
    {

        // 참고!!
        // 요약:
        //     System.Net.Sockets.NetworkStream에서 데이터를 읽습니다.
        //
        // 매개 변수:
        //   buffer:
        //     System.Byte에서 읽은 데이터를 저장하기 위한 메모리 내의 위치에 해당하는 System.Net.Sockets.NetworkStream
        //     형식의 배열입니다.
        //
        //   offset:
        //     데이터를 저장하기 시작하는 buffer 내의 위치입니다.
        //
        //   size:
        //     System.Net.Sockets.NetworkStream에서 읽을 바이트 수입니다.
        //
        // 반환 값:
        //     System.Net.Sockets.NetworkStream에서 읽은 바이트 수이며 소켓이 닫힌 경우 0입니다.
        //
        // 예외:
        //   T:System.ArgumentNullException:
        //     buffer 매개 변수가 null인 경우
        //
        //   T:System.ArgumentOutOfRangeException:
        //     offset 매개 변수가 0보다 작은 경우 
        //     또는 offset 매개 변수가 buffer의 길이보다 큰 경우 
        //     또는 size 매개 변수가 0보다 작은 경우
        //     또는 size 매개 변수가 buffer의 길이에서 offset 매개 변수의 값을 뺀 값보다 큰 경우 
        //     또는 소켓에 액세스할 때 오류가
        //     
        //     발생했습니다.
        //
        //   T:System.IO.IOException:
        //     내부 System.Net.Sockets.Socket이 닫힌 경우
        //
        //   T:System.ObjectDisposedException:
        //     System.Net.Sockets.NetworkStream가 닫힌 경우 
        //     또는 네트워크에서 읽는 동안 오류가 발생한 경우
        // 기본 Read 함수 원형
        //public override int Read(byte[] buffer, int offset, int size);

        
        // .Read는 원격 연결이 해제되면 '0'을 반환
        // 하지만 소유한 연결 중 강제로 연결을 닫은 경우 IOException 예외를 던짐
        //
        // 두 경우 모두 '0'을 던지도록 ReadSafely 을 추가
        // 연결 해제는 연결 해제니까 .. 예외를 고려할 필요가 있음
        public static int ReadSafely(this NetworkStream stream, byte[] buffer, int offset, int size)
        {
            try
            {
                return stream.Read(buffer, offset, size);
            }
            catch (IOException)
            {
                return 0;
            }
        }

        // 정확히 'n'만큼 읽어오는 도우미 함수
        // -> 기본적인 .Read는 'n' bytes 보다 크게 읽어옴, 해당 함수는 정확히 'n'만큼 읽어옴
        // -> 'n' bytes 만큼 받을 때까지 blocking!
        // -> 연결 해제인 경우 즉시 false 반환
        public static bool ReadExactly(this NetworkStream stream, byte[] buffer, int amount)
        {
            // TCP buffer에는 .Read 함수에서 한번에 전체의 양을 읽을 만큼의 bytes가 충분치 않음
            // 그렇기에 모든 bytes를 가져올 때까지 시도할 필요가 있음 (물론 blocking 상태로!)
            //
            // 노트: 이는 다른 걸 읽고 나서부터 더욱 빠른 버전임 
            //for(int i = 0; i < amountl; ++i)
            //    if(stream.Read(buffer, i, 1) == 0)
            //        return false;
            //    return true;

            int bytesRead = 0;
            while (bytesRead < amount)
            {
                // 'safe'한 읽기 확장일 경우 'remaining' bytes를 읽어옴
                int remaining = amount - bytesRead;
                int result = stream.ReadSafely(buffer, bytesRead, remaining);

                // 연결이 해제된 경우엔 .Read는 0을 반환
                if (result == 0)
                {
                    return false;
                }

                // 마찬가지로 읽어온 bytes을 더함
                bytesRead += result;
            }
            return true;
        }
    }

}

