using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Meokgoeeum
{
    /// <summary>
    /// TutorialSkipManager (튜토리얼 스킵 확인창)
    /// [[15 튜토리얼 설계]] "튜토리얼 스킵" — ESC → 스킵 팝업 노출 → 확인 시 스킵 + 1층 시작 지점으로
    /// 이동, 취소 시 튜토리얼 계속.
    ///
    /// ⚠️ `SettingsMenu`가 이미 Esc를 "설정 메뉴 열기"로 쓰고 있어서 충돌합니다. 그래서 이 스크립트는
    /// 자기 자신의 Esc 폴링을 하지 않고, `SettingsMenu.Update()`가 대신 판단해서
    /// (`SkipAvailable`이면 이쪽을, 아니면 자기 자신을 열게) 호출해주는 구조로 만들었습니다 —
    /// Esc를 두 스크립트가 각자 폴링하면 같은 프레임에 둘 다 열리는 경쟁 상태가 생기기 때문입니다.
    ///
    /// "1층 시작 지점"은 `Floor1CorridorBuilder`가 만든 `Checkpoint_ObstaclePassed`(장애물 통과
    /// 직후, 방A 진입 직전) 위치로 정의했습니다 — 별도 좌표를 하드코딩하지 않고 그 오브젝트
    /// 위로 순간이동시키면, 그 체크포인트 자신의 트리거가 다음 프레임에 자동으로 발동해서
    /// 방A 전투 시작(`EncounterSpawner.StartEncounter()`)까지 기존 로직 그대로 재사용됩니다.
    /// (`CubeMapManager.TeleportPlayerToSpawnPoint()`와 같은 "이름으로 찾기" 관례를 따름 — 씬 에디팅
    /// 시점엔 SC_Game/SC_Face가 동시에 열려있지 않아 직접 참조를 직렬화할 수 없어서, 런타임에
    /// `GameObject.Find`로 찾습니다.)
    /// </summary>
    public class TutorialSkipManager : MonoBehaviour
    {
        public static TutorialSkipManager Instance { get; private set; }

        [Tooltip("스킵 확인 후 이 이름의 오브젝트 위치로 순간이동합니다.")]
        public string skipDestinationObjectName = "Checkpoint_ObstaclePassed";

        public bool SkipAvailable { get; private set; } = true;
        public bool IsShowing { get; private set; }

        private GameObject panel;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[TutorialSkipManager] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
                Destroy(gameObject);
                return;
            }

            EnsureEventSystem();
            BuildUI();
            panel.SetActive(false);
        }

        /// <summary>[[SettingsMenu]]가 씬에 하나만 만들면 되지만, Awake 순서가 보장되지 않아 방어적으로 여기서도 확인합니다.</summary>
        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>이 구간(1층 입장 복도)을 이미 정상적으로 통과했을 때 [[TutorialCheckpoint]]가 호출합니다 — 통과한 뒤엔 스킵을 다시 제안할 이유가 없습니다.</summary>
        public void DisableSkip()
        {
            SkipAvailable = false;
        }

        /// <summary>[[SettingsMenu]]의 Esc 핸들러가 SkipAvailable일 때 대신 호출합니다.</summary>
        public void ShowSkipConfirm()
        {
            IsShowing = true;
            panel.SetActive(true);
            Time.timeScale = 0f;
        }

        /// <summary>"취소" — 튜토리얼 계속. [[SettingsMenu]]가 팝업이 떠있는 동안의 Esc도 이걸로 라우팅합니다.</summary>
        public void Cancel()
        {
            IsShowing = false;
            panel.SetActive(false);
            Time.timeScale = 1f;
        }

        /// <summary>"확인" — 스킵 + 1층 시작 지점(방A 진입 직전)으로 이동.</summary>
        public void Confirm()
        {
            IsShowing = false;
            panel.SetActive(false);
            Time.timeScale = 1f;
            StartCoroutine(SkipRoutine());
        }

        private IEnumerator SkipRoutine()
        {
            DisableSkip(); // 근본적으로도 Checkpoint_ObstaclePassed 트리거가 다시 이걸 부르겠지만, 순간이동 도중 다시 스킵 버튼을 누르는 걸 막기 위해 즉시 끔.

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            GameObject destination = GameObject.Find(skipDestinationObjectName);

            if (player == null || destination == null)
            {
                Debug.LogWarning($"[TutorialSkipManager] Player 또는 {skipDestinationObjectName}을(를) 못 찾아 스킵 이동을 취소합니다.");
                yield break;
            }

            if (FadeManager.Instance != null) yield return FadeManager.Instance.FadeOut();

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Vector3 oldPos = player.transform.position;
            player.transform.position = destination.transform.position;
            if (cc != null) cc.enabled = true;

            // [[changelog/2026-09-10_카메라-순간이동추적버그-수정]] CubeMapManager와 같은 이유 —
            // Cinemachine에 순간이동을 알려주지 않으면 팔로우 카메라가 천천히 쫓아가려다 길을 잃습니다.
            CinemachineCore.OnTargetObjectWarped(player.transform, destination.transform.position - oldPos);

            if (FadeManager.Instance != null) yield return FadeManager.Instance.FadeIn();

            Debug.Log("[TutorialSkipManager] 튜토리얼 스킵! 1층 시작 지점(방A 진입 직전)으로 이동함.");
        }

        private void BuildUI()
        {
            var canvasObj = new GameObject("TutorialSkipCanvas");
            canvasObj.transform.SetParent(transform, false);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950; // SettingsMenu와 같은 급 — 상호 배타적이라 겹칠 일 없음

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            panel = new GameObject("Panel");
            panel.transform.SetParent(canvasObj.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.85f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.28f, 0.35f);
            panelRect.anchorMax = new Vector2(0.72f, 0.65f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            CreateText("Question", panel.transform, "튜토리얼을 건너뛸까요?", 22, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.9f));

            BuildButton("ConfirmButton", new Vector2(0.08f, 0.1f), new Vector2(0.46f, 0.4f),
                "건너뛰기", new Color(0.8f, 0.3f, 0.3f), Confirm);
            BuildButton("CancelButton", new Vector2(0.54f, 0.1f), new Vector2(0.92f, 0.4f),
                "계속하기", new Color(0.3f, 0.5f, 0.8f), Cancel);
        }

        private void BuildButton(string name, Vector2 anchorMin, Vector2 anchorMax, string label, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(panel.transform, false);
            var img = buttonObj.AddComponent<Image>();
            img.color = color;
            var rect = buttonObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);

            CreateText("Label", buttonObj.transform, label, 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize,
            TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = content;
            text.raycastTarget = false; // 버튼 라벨은 클릭을 부모(버튼)로 그대로 통과시켜야 함

            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
