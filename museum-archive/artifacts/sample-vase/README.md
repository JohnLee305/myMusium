# 샘플 항아리

실제 소장품이 아닌 개발용 샘플이다. tools/create-sample-vase.py에서 회전체 메시와 청록색 재질을 직접 생성했다. 외부 모델/사진/텍스처를 사용하지 않았다.

- 높이: 약 0.445m
- 모델: models/pc.glb
- 메타데이터: artifact.json
- 실행 시 scale을 적용하고 모델 밑면을 전시대에 맞춘다.
- 다시 생성: Blender --background --factory-startup --python-exit-code 1 --python tools/create-sample-vase.py

원본 스캔 보존이나 모바일 최적화의 사례로 간주하지 않는다. 이후 실제 물건을 스캔하면 별도 ID의 전시품으로 등록한다.
