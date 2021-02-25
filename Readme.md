# Unity Networking practice

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

#### 2021.02.19

kcp Transport가 Telepathy의 후속작이라는 충격적인 사실을 알게 됐다. 지금껏 이미 지난 버전을 바탕으로 학습하고 있었다. WOW! 😁

다음 프로젝트의 네트워킹 부분은 일부 Mirror 패키지와 Redwood Transport를 사용하겠다.

네트워킹이 원활하게 진행되기 위해 많은 부분이 필요함을 알았다. Network Manager만 있으면 되는 줄 알았다. 다음 시간에는 Mirror Runtime 폴더에서 클래스들의 역할을 파악하기로 한다.

[Update] .gitignore을 Unity용으로 수정



## Redwood Network 사용 불가!

Kcp Transport를 사용하도록 하자.. Mirror의 Network Manager는 Redwood Transport 입력 시 Kcp Transport가 자동 생성 및 추가. 이에 일부 코드의 디렉터리를 수정 및 주석(deprecated, 사용 안함) 추가



## 새로운 미니 게임 개발

Mirror 네트워크를 사용한 타일을 클릭해 뒤집으면 색이 바뀌는 간단한 게임

- 1:1과 Melee(개인전)으로 구성할 예정

#### 2021.02.23

멀티 에디터 [ParrelSync](https://github.com/VeriorPies/ParrelSync) 패키지 추가

#### 2021.02.25

서버 / 클라이언트 진행 중


[NetworkManagerCallbacks](https://docs.unity3d.com/2019.3/Documentation/Manual/NetworkManagerCallbacks.html) 네트워크 매니저 콜백 함수(폐기된 UNet의 문서, 참고용)

추가할 사항

- 진영의 수가 2 이상모였을 때 모든 진영에서 준비 완료 신호 보내면 게임 시작하는 기능(진영은 최대 4개까지 추가할 예정)
- 서버 측에서 타이머가 돌며 각 진영에서 활성화시킨 깃발의 수 표시
- 서버 혹은 클라이언트 연결 해제시 처리

