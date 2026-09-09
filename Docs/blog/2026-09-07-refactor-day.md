# 먹괴음 개발일지 - 리팩토링 하루종일, EnemyBase부터 자동 테스트까지

스크립트 59개, 총 7,202줄이 전부 `Assets/Scripts` 한 폴더에 평평하게 쌓여 있었다. namespace 0개, asmdef 0개, 자동 테스트 0개.

며칠 전부터 벼르던 리팩토링을 오늘 몰아서 했다. 계획은 6단계였는데, 진행하면서 계획 자체가 몇 번 바뀌었다.

## 몹 5종 복붙 코드부터

`EnemyGwang`, `EnemyBun`, `EnemyWon`, `EnemyPyeong`, `EnemyHeup` — 5종 몹 파일을 나란히 열어보니 `Awake`, `UpdatePerception`, `ApplyKnockback`, `KnockbackRoutine`, `OnDrawGizmosSelected`가 거의 그대로 복붙돼 있었다.

전투 상태머신은 종별로 진짜 다르다. 원은 Active 상태 자체가 없고, 흡은 아예 비공격 유닛이고, 광은 사거리 대신 쿨다운으로 공격을 트리거한다. 그래서 그건 안 건드리고, 인프라만 뺐다.

```csharp
// EnemyBase.cs
public abstract class EnemyBase : MonoBehaviour, IKnockbackable
{
    protected void TickPerception() { ... }
    public void ApplyKnockback(Vector3 direction, float force) { ... }
    protected virtual void OnDrawGizmosSelected() { ... }
}
```

재밌는 건, 분과 평은 전투 로직이 완전히 동일한데도 `EnemyBun.cs`에 이미 이런 주석이 있었다.

> 지금 규모에서 상속/공유 베이스 클래스로 묶는 것보다 단순 복제가 유지보수하기 더 쉽다고 판단했습니다.

과거의 판단을 존중해서 그건 그대로 뒀다. 인프라 코드 통합이랑 전투 로직 설계 판단은 별개니까.

## 폴더 정리하다가 튀어나온 순환 참조

51개 파일을 `Enemies/`, `UI/`, `Managers/` 같은 12개 폴더로 옮기고 전체에 `namespace Meokgoeeum`을 씌웠다. 여기까진 순조로웠다.

문제는 asmdef를 나누면서 시작됐다. `Meokgoeeum.Runtime.asmdef`를 만들자마자 컴파일이 깨졌다.

```
error CS0246: The type or namespace name 'PlayerInputActions' could not be found
```

Input System이 생성한 `PlayerInputActions.cs`가 `Assets/Input/`에, 즉 `Assets/Scripts/` 밖에 있었다. 이름 있는 asmdef는 암묵적 어셈블리보다 항상 먼저 컴파일되기 때문에, 밖에 있는 파일을 참조할 방법이 없었다. `Assets/Scripts/Input/`으로 옮기고 나서야 풀렸다.

여기서 욕심을 좀 냈다. 폴더별로 더 잘게 쪼개고 싶어서 실제 참조 관계를 스크립트로 뽑아봤다.

```
Bosses -> Enemies, Enemies -> Bosses
Enemies -> Level, Level -> Enemies
Level -> Save, Save -> Level
...
```

거의 모든 폴더 쌍이 서로를 참조하고 있었다. 하나만 봐도 이 정도다.

```
Level/RoomClearGate.cs  → Enemies/EnemyHealth
Enemies/EnemyHeup.cs    → Level/RestoredAreaRegistry
```

이 정도 순환이면 파일 몇 개 옮기는 걸로 안 풀린다. 인터페이스나 이벤트로 역참조를 끊는 설계 작업이 따로 필요한 수준이라 여기서 접었다. `Meokgoeeum.Runtime` / `Meokgoeeum.Editor` 두 어셈블리로만 나누고, 폴더별 세분화는 나중에 빌드 시간이 실제로 문제될 때 다시 보기로.

## FindObjectOfType 24곳, 다 걷어내진 않았다

`FindObjectOfType`이 24곳 있었는데, 까보니 성격이 셋으로 나뉘었다.

싱글턴이 없어서 매번 씬을 훑던 곳 — `PlayerHealth`, `BossHealth`, `LockOnController`, `MouseSensitivitySetting`. 이미 있는 매니저 13개와 같은 `static Instance` 패턴을 붙여서 4곳을 해결했다.

"씬에 EventSystem이 있는지" 확인하는 부트스트랩 체크 2곳은 애초에 대체할 직렬화 참조가 없는 정상적인 패턴이라 API만 최신으로 바꿨다.

나머지 `GameObject.Find("SpawnPoint")` 같은 것들은 코드에 이유가 이미 적혀 있었다.

> SC_Game/SC_Face가 동시에 열려있지 않아 직접 참조를 직렬화할 수 없어서, 런타임에 GameObject.Find로 찾습니다.

씬 구조상 진짜로 다른 방법이 없는 경우라 그대로 뒀다.

## 처음 만든 자동 테스트가 알려준 것

이 프로젝트에 자동 테스트가 하나도 없었다. `EnemyHealth`, `PlayerHealth`, `BossHealth`의 피격/회복/사망 로직부터 22개를 짰다.

돌렸더니 12개가 실패했다. 게임 로직이 잘못된 줄 알고 코드를 다시 뜯어봤는데, 진짜 원인은 따로 있었다.

```
Expected: 20.0f
But was:  0.0f
```

EditMode 테스트에서는 `AddComponent` 직후에 `Awake()`가 바로 안 불린다. Play 모드에서는 즉시 불리는데, 에디터 테스트는 다음 틱까지 미룬다. `currentHP`가 아직 초기화가 안 된 채로 검사하고 있었던 거다.

```
Unhandled log message: 'Destroy may not be called from edit mode!'
```

이건 `EnemyHealth.Die()`가 부르는 `Destroy()`가 EditMode에서는 에러 로그를 남기는 것 때문이었다. 실제로 파괴는 안 되지만, 그 로그 자체를 테스트 프레임워크가 실패로 잡는다.

둘 다 리플렉션으로 `Awake()`를 직접 호출하고, 죽음을 유발하는 테스트에는 `LogAssert.Expect`로 그 로그를 미리 예상해두는 걸로 해결했다. 고치고 나니 22개 전부 통과. 나중에 `ColorSystemManager`, `GameState`까지 추가해서 지금은 35개다.

## QA 문서 하나

리팩토링이 끝난 김에 QA 테스트 케이스 문서도 만들었다. 60개 케이스에, 코드 주석에 남아있던 과거 버그 9건을 회귀 케이스로 명시해뒀다. TC ID, 우선순위, 자동화 여부까지 갖춰서 실제로 QA에 넘길 수 있는 형태로.

## 오늘 결과

- `EnemyBase` 공통 클래스 추출
- 폴더 12개로 정리, 전체 namespace 적용
- asmdef 2개로 분리, 폴더별 세분화는 순환 참조 때문에 보류
- 싱글턴 4개 추가, deprecated API 정리
- 자동 테스트 35개
- QA 테스트 케이스 60개

작업 도중에 에디터가 크래시 로그 하나 없이 조용히 꺼진 적이 있었다. 재실행하니 멀쩡했는데, 원인은 아직 모른다. 다음에 또 그러면 그때 파봐야겠다.
