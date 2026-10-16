# Aquarium 1.8 생물 군락과 메시 보수

말미잘의 최종 형태·색상 기준은 사용자가 제공한 두 사진입니다.

- `codex-clipboard-97494c27-fad5-4bb7-826f-7090bb9fd496.png`: 붉은 기둥과 부드럽게 휘는 분홍 촉수.
- `codex-clipboard-60ddc18c-5361-4893-ba21-dd3f2a557a07.png`: 올리브색 기둥과 짧고 둥근 연두·아이보리 촉수. 사진 속 물고기는 추가로 모델링하지 않았습니다.

보조 생태 자료: [Monterey Bay Aquarium — Giant green anemone](https://www.montereybayaquarium.org/animals-the-ocean/animals-a-to-z/giant-green-anemone). 외부 사진을 텍스처로 복사하지 않았으며 최종 외형은 사용자의 두 레퍼런스를 우선했습니다.

이번 버전의 산호·해조류·말미잘은 프로젝트 내부 C# 메시 생성 코드로 직접 제작했습니다. 새로운 외부 이미지·3D 모델·생성형 비트맵은 추가하지 않았습니다. 기존 4K 암석·모래 재질과 전시 그림은 유지합니다.

삼각형 방향·연결 검사와 실제 Unity 렌더링 기록은 `Artifacts/CoralGallery/biodiversity-validation.txt` 및 `living-motion-validation.txt`에 있습니다. 아래는 유지한 1.7·1.6 재질의 출처 기록입니다.

# Aquarium 1.7 모래·암반 레퍼런스

- 수중 실루엣: 사용자가 제공한 `codex-clipboard-1f936454-60f1-4a53-b4eb-af246cf8560a.png`. 오른쪽의 돌출된 절벽, 움푹 파인 하부와 불규칙한 발치 돌무더기를 모델링 기준으로 사용했습니다. 사진 속 인물은 모델링하지 않았습니다.
- 모래 환경 참고: [Monterey Bay Aquarium — Sandy seafloor](https://www.montereybayaquarium.org/animals-the-ocean/ecosystems/sandy-seafloor). 모래·퇴적물·부서진 조개 입자, 조류에 따라 달라지는 바닥 형태를 참고했습니다. 웹 이미지를 게임 텍스처로 복제하지 않았습니다.
- 대형 수조 참고: [Georgia Aquarium — Ocean Voyager](https://www.georgiaaquarium.org/gallery/ocean-voyager/).
- 새 미세 모래 재질: [Poly Haven — Sand 02](https://polyhaven.com/a/sand_02), Charlotte Baglioni, [CC0](https://polyhaven.com/license). 약 2.1 m 폭을 촬영한 재질의 4K 색상·노멀 및 2K ARM을 사용했습니다. 기존 30 m 폭 항공 촬영 재질과 달리 모래 입자에 맞춘 크기입니다.
- 게임에서는 `GranularSand.shader`로 두 방향·크기의 입자를 혼합하고, 밝은 모래색으로 조정합니다. 다운로드 파일 자체는 수정하지 않았습니다.
- 신규 저장 파일: `Assets/Demos/HideAndSeek/Environment/Resources/Aquarium/Textures/SandGrainAlbedo.jpg`, `SandGrainNormal.jpg`, `SandGrainARM.jpg`.
- 암반 모델은 코드로 직접 제작한 절리 입체입니다. 암석 표면 텍스처는 아래 Rock Face 03을 계속 사용합니다.

다운로드 검증 기록:

```json
[
  {
    "file": "SandGrainAlbedo.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/4k/sand_02/sand_02_diff_4k.jpg",
    "md5": "794702cb43a3e6935e5dfb4dc21c7ee0",
    "bytes": 17410069
  },
  {
    "file": "SandGrainNormal.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/4k/sand_02/sand_02_nor_gl_4k.jpg",
    "md5": "55c152cdca3093d0db693043ebd07c72",
    "bytes": 23188393
  },
  {
    "file": "SandGrainARM.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/sand_02/sand_02_arm_2k.jpg",
    "md5": "f09c4369f5573d3c11fcf65d8e1069da",
    "bytes": 2525395
  }
]
```

## 이전 1.6 재질·전시 그림 기록



## 촬영 기반 PBR 재질

- 암석: [Rock Face 03 — Poly Haven](https://polyhaven.com/a/rock_face_03). 사진 Dario Barresi, 처리 Rico Cilliers.
- 모래: [Aerial Beach 01 — Poly Haven](https://polyhaven.com/a/aerial_beach_01). Rob Tuytel.
- 라이선스: [Poly Haven CC0](https://polyhaven.com/license). 상업 프로젝트 사용 및 재배포 가능.
- 원본 4K 색상/일반 RGB OpenGL 노멀, 2K ARM(빨강 AO, 초록 거칠기, 파랑 금속성)을 다운로드했으며 API에서 제공하는 MD5와 대조했습니다. 단순 업스케일이 아닙니다.
- 저장 위치: `Assets/Demos/HideAndSeek/Environment/Resources/Aquarium/Textures/`의 `RockAlbedo.jpg`, `RockNormal.jpg`, `RockARM.jpg`, `SandAlbedo.jpg`, `SandNormal.jpg`, `SandARM.jpg`.
- 월드 좌표 기반 삼면 투영, 별도 미세 표면 노멀, 거칠기·환경 폐색, 밉맵과 16배 비등방성 필터를 적용했습니다. 게임 빌드는 BC7 압축을 사용합니다.

## 전시 그림

- 생성 모드: **built-in image generation** (`image_gen__imagegen`).
- 생성일: 2026-10-16. 한 번 생성한 원본 결과를 그대로 사용했습니다.
- 최종 저장 위치: `C:/Users/shern/Documents/Demo/Assets/Demos/HideAndSeek/Environment/Resources/Aquarium/Textures/MarineExhibitAtlas.png`.
- 실제 원본 해상도: 1448×1086. 2×2 아틀라스를 네 전시 패널로 나눠 사용합니다. 4K라고 표기하는 재질은 위 암석·모래 색상/노멀 맵에 한정합니다.
- 생성 원본: `C:/Users/shern/.codex/generated_images/01a142f7-4773-7482-9c38-e2e9de669737/exec-2f546ae5-7b6b-445c-b22a-91478df51f89.png`.
- 제목·설명은 그림에 포함하지 않고 Unity TextMesh로 따로 표시했습니다.

최종 프롬프트:

```text
Use case: scientific-educational. Asset type: a single high-resolution aquarium exhibition illustration atlas for a 3D game, intended to be displayed as illuminated visitor gallery graphics. Create exactly a 2 by 2 grid of four equal rectangular illustrated exhibit panels, aligned to the image quarters with absolutely no outer margin and no thick borders. Overall image aspect 4:3, maximum native resolution, crisp fine detailed imagery suitable for close-up texture viewing. Each quarter uses the same very dark midnight navy background and museum-grade natural history scientific illustration style, delicate luminous colored drawing and subtle elegant diagram lines. Upper left: richly detailed branching corals, small reef fish, sponges, shells in a reef ecosystem study. Upper right: a graceful manta ray as the large hero with several small schooling fish around it. Lower left: exquisite translucent jellyfish, drifting particles, deep ocean bioluminescent forms. Lower right: elaborate nautilus shell cross-section, sea star, anemone and marine invertebrates. The upper 18 percent of each quarter is mostly empty navy negative space to overlay a crisp title in the game; keep main illustrations in the central/lower 75 percent. Clear hierarchy, restrained turquoise, warm ivory, muted orange and pale gold accents. Keep illustrations prominent and complex, more like an expensive natural history museum exhibit than a cartoon. NO people, NO room, NO frame, NO perspective, NO mockup, NO watermarks, NO lettering or words; graphic art only.
```

## 다운로드 검증 기록

```json
[
  {
    "file": "RockAlbedo.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/4k/rock_face_03/rock_face_03_diff_4k.jpg",
    "bytes": 15319503,
    "md5": "50105980b509029b9040f43b1f6ddc71"
  },
  {
    "file": "RockNormal.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/4k/rock_face_03/rock_face_03_nor_gl_4k.jpg",
    "bytes": 16283883,
    "md5": "2408a373765c41a7a3762933ea67f5b0"
  },
  {
    "file": "RockARM.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/rock_face_03/rock_face_03_arm_2k.jpg",
    "bytes": 3215097,
    "md5": "f7999b2d14ebbea91e1a5adfb7beabc3"
  },
  {
    "file": "SandAlbedo.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/4k/aerial_beach_01/aerial_beach_01_diff_4k.jpg",
    "bytes": 1614021,
    "md5": "cb57acaebe3a54d2b698987fb90cc249"
  },
  {
    "file": "SandNormal.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/4k/aerial_beach_01/aerial_beach_01_nor_gl_4k.jpg",
    "bytes": 2435079,
    "md5": "997e9069d94933ab4fdc0826d6166e9a"
  },
  {
    "file": "SandARM.jpg",
    "url": "https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/aerial_beach_01/aerial_beach_01_arm_2k.jpg",
    "bytes": 760953,
    "md5": "c45cbda135f39549166a15596ce995e1"
  }
]
```
