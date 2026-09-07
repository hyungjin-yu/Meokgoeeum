using UnityEngine;
using UnityEngine.UI;

namespace Meokgoeeum
{
    /// <summary>
    /// LockOnMarkerUI (락온 타겟 표시)
    /// [[LockOnController]]가 카메라 프레이밍 없이도 "지금 뭘 락온했는지" 알 수 있게, 화면상 타겟
    /// 위치에 작은 마커를 띄웁니다. 아트 에셋이 없어서 그냥 회전하는 사각형 하나로 "조준경" 느낌만
    /// 냅니다.
    ///
    /// UI를 손으로 만들지 않고 Awake()에서 코드로 직접 생성합니다(다른 매니저들과 같은 이유).
    /// </summary>
    public class LockOnMarkerUI : MonoBehaviour
    {
        public static LockOnMarkerUI Instance { get; private set; }

        [Tooltip("마커가 회전하는 속도입니다. (초당 각도)")]
        public float spinSpeed = 90f;

        [Tooltip("마커를 타겟 위치에서 위로 얼마나 띄울지입니다 (발밑이 아니라 몸통쯤에 뜨도록).")]
        public float heightOffset = 1.2f;

        private RectTransform markerRect;
        private Camera mainCamera;
        private LockOnController lockOn;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("[LockOnMarkerUI] 이미 인스턴스가 존재합니다. 중복 오브젝트를 파괴합니다.");
                Destroy(gameObject);
                return;
            }

            BuildUI();
        }

        private void BuildUI()
        {
            var canvasObj = new GameObject("LockOnMarkerCanvas");
            canvasObj.transform.SetParent(transform, false);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 890; // HintPopupManager(910)/NarrationManager(900)보다 아래 — 게임플레이 요소 취급

            canvasObj.AddComponent<CanvasScaler>();

            var markerObj = new GameObject("Marker");
            markerObj.transform.SetParent(canvasObj.transform, false);
            var image = markerObj.AddComponent<Image>();
            image.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            image.raycastTarget = false;

            markerRect = image.rectTransform;
            markerRect.sizeDelta = new Vector2(36f, 36f);

            markerObj.SetActive(false);
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (lockOn == null) lockOn = LockOnController.Instance;

            if (mainCamera == null || lockOn == null || !lockOn.IsLockedOn)
            {
                if (markerRect.gameObject.activeSelf) markerRect.gameObject.SetActive(false);
                return;
            }

            Vector3 worldPos = lockOn.CurrentTarget.position + Vector3.up * heightOffset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0f) // 타겟이 카메라 뒤에 있으면(락온 중 카메라를 반대로 돌린 경우 등) 숨김
            {
                if (markerRect.gameObject.activeSelf) markerRect.gameObject.SetActive(false);
                return;
            }

            if (!markerRect.gameObject.activeSelf) markerRect.gameObject.SetActive(true);
            markerRect.position = screenPos;
            markerRect.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        }
    }
}
