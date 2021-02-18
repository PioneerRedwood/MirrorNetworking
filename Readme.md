## Unity Networking practice

#### 2021.02.16

- Unity 내 네트워크 모듈이었던 UNet을 이어받은 [Mirror 네트워킹](https://mirror-networking.com/) 방식을 토대로 학습


- Mirror 중 전송 방식이 TCP이며 MMORPG 네트워크 규모를 대상으로 개발된 Telepathy를 채택했다.

#### 2021.02.17

Server 클래스 분석 ... 이름만 분석이지 주석을 한글로 번역하고 고개 끄덕임의 반복이다..

- C# 지식 확장
  - [Threading C# Microsoft Docs](https://docs.microsoft.com/ko-kr/dotnet/api/system.threading.thread?view=net-5.0)
    - ArraySegment
  - [delegate](https://docs.microsoft.com/ko-kr/dotnet/csharp/programming-guide/delegates/) Action<> , Func<>
- Server 관련 유틸리티 클래스
  - ThreadFuction

#### 2021.02.18

Server 클래스 마무리, Client 분석

- C# 지식 확장
  - [volatile](https://docs.microsoft.com/ko-kr/dotnet/csharp/language-reference/keywords/volatile), [ManualResetEvent](https://docs.microsoft.com/ko-kr/dotnet/api/system.threading.manualresetevent?view=net-5.0) 수동 초기화 이벤트 등
- 유니티 확장
  - 유니티 내에서 pc 버전으로 빌드하고 에디터 내 실행해서 서버와 클라이언트가 정상적으로 동작하는 것을 확인!
  - Mirror에선 Transport라는 것과 매니저를 두고 처리한다. 다음엔 이렇게 구체적으로 어떻게 통신하는 지 Mirror 패키지를 살펴볼 생각이다.