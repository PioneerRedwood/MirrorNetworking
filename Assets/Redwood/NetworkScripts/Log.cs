// 2021.02.22 편집
// 학습용으로 만들어졌으므로 사용할 수 없음

// Console.WriteLine을 기본으로 사용하는 단순 Logger 클래스
// 또한 Logger.LogMethod = Debug.Log 유니티 내에서 사용 가능
// UnityEngine.DLL에 의존할 필요 없으며 UnityEngine의 모든 버전이 필요 없음

using System;

namespace Redwood
{
    public static class Log
    {
        public static Action<string> Info = Console.WriteLine;
        public static Action<string> Warning = Console.WriteLine;
        public static Action<string> Error = Console.Error.WriteLine;
    }
}
