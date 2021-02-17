using System;
using System.Collections.Generic;

namespace Redwood
{
    // 할당을 피하기 위한 풀. originally from libuv2k
    public class Pool<T>
    {
        // 객체들
        readonly Stack<T> objects = new Stack<T>();

        // 몇몇 타입은 적절한 생성자를 위해 추가적인 매개 변수가 필요
        // 그리하여 Fuc<T> 생성자를 사용
        readonly Func<T> objectGenerator;

        // 생성자
        public Pool(Func<T> objectGenerator)
        {
            this.objectGenerator = objectGenerator;
        }

        // 풀에서 요소 가져오기 혹은 빈 요소 생성
        public T Take() => objects.Count > 0 ? objects.Pop() : objectGenerator();

        // 풀에서 요소 반환
        public void Return(T item) => objects.Push(item);

        // 풀 정리. 그리고 각 객체들에 적용하기
        public void Clear() => objects.Clear();

        // 풀에 얼마나 많은 요소가 있는지 확인. 테스트하기에 용이
        public int Count() => objects.Count;
    }
}
