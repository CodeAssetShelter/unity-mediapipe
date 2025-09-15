# 📌 unity-mediapipe

MediaPipe 의 HandLandmark 를 이용한 손 쥐기/펴기 인식 프로젝트

---

## 📝 개요

- 손 쥐기/펴기 인식, Palm 중앙에 RectTransform 를 위치
- Unity >= 2022.3
- [플러그인 링크](https://github.com/homuler/MediaPipeUnityPlugin)

---

## 🚀 사용방법

- 깃 클론 또는 프로젝트 전체를 받은 뒤, Assets/Hand Landmarks.unity 열기

## 🎈 비고
- 각 이벤트 버스 2종 (HandLandmarkPointBus, HandResultBus) 은 각각 HandLandmarkerResultAnnotationController, HandLandmarkListAnnotation 에 위치합니다.<br>
  해당 스크립트는 MediaPipe 패키지 내에 있으며, 필요시 주 사용 스크립트로 옮겨서 사용하세요.<br>
  두 이벤트는 랜드마크의 정규화 된 포인터 값과 Palm 의 중심값을 계산하기 위한 각 (0 : Wrist // 5, 9, 13, 17 : MCP) 의 값을 불러옵니다.

### 설치
```bash
git clone https://github.com/CodeAssetShelter/unity-mediapipe.git
cd repository
