using DG.Tweening;
using UnityEngine;

namespace ElementalBreaker
{
    // DOTween을 첫 씬이 열리기 전에 한 번 준비해. 하이어라키를 건드리지 않게 코드로만 해.
    // 트윈을 재사용하니까, 끝난 트윈을 변수에 들고 있지 말고 대상(DOKill, DOTween.Kill)으로 멈춰.
    internal static class TweenSetup
    {
        // 동시에 도는 트윈 수보다 넉넉하게 잡아서, 실행 중에 목록이 늘어나며 메모리를 새로 잡지 않게 해.
        private const int TweenerCapacity = 64;
        private const int SequenceCapacity = 8;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // 끝난 트윈은 버리지 않고 다시 써. 대상이 사라져도 오류 없이 넘어가게 안전 모드를 켜.
            DOTween.Init(true, true, LogBehaviour.ErrorsOnly).SetCapacity(TweenerCapacity, SequenceCapacity);
        }
    }
}
