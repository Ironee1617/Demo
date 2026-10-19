# 한국어 UI 폰트 출처

로그라이크 UI는 **NanumGothic Regular(나눔고딕)**를 사용합니다. 기존 Windows 맑은 고딕을 공식 Google Fonts 저장소에서 받은 정적 TrueType 폰트로 교체했습니다. 폰트 바이트나 내부 이름을 변경하지 않았으며 Unity 리소스 경로만 `Resources/Tower/Font.ttf`로 유지합니다.

- 공식 저장소: [google/fonts](https://github.com/google/fonts/tree/47d54ca1ebb14fd864695f1d39dc734f39017c33/ofl/nanumgothic)
- 고정 원본 커밋: `47d54ca1ebb14fd864695f1d39dc734f39017c33`
- 폰트 원본: [NanumGothic-Regular.ttf](https://raw.githubusercontent.com/google/fonts/47d54ca1ebb14fd864695f1d39dc734f39017c33/ofl/nanumgothic/NanumGothic-Regular.ttf)
- 원문 라이선스: [OFL.txt](https://raw.githubusercontent.com/google/fonts/47d54ca1ebb14fd864695f1d39dc734f39017c33/ofl/nanumgothic/OFL.txt)
- 저작권: Copyright (c) 2010, NHN Corporation
- 라이선스: SIL Open Font License 1.1. 원문을 `Assets/Demos/Roguelike/Resources/Tower/Font-License.txt`에 그대로 동봉합니다.

## 파일 검증

| 파일 | 바이트 | SHA-256 |
| --- | ---: | --- |
| `Font.ttf` | 2,054,744 | `76f45ef4a6bcff344c837c95a7dcc26e017e38b5846d5ae0cdcb5b86be2e2d31` |
| `Font-License.txt` | 4,534 | `eeacf16032901d0ed0456876ec77b8f0fda6b3fecec7d972f8543eb602e6c30f` |

다운로드 응답이 실제 TrueType 형식이고 `cmap`, `name`, `glyf` 테이블이 있는지 확인했습니다. 폰트 내부 영문 패밀리는 `NanumGothic`입니다. Unicode 매핑에서 현대 한글 완성형 U+AC00–U+D7A3의 **11,172개 음절 전부**와 영문·숫자·일반 UI 기호의 글리프를 확인했습니다.

기존 `Font.ttf.meta` GUID `4ea9a972fd96405c8f3f0371a701586f`를 보존하고 importer의 패밀리 이름만 갱신하여 기존 Unity 리소스 참조를 유지합니다. 폰트 파일은 공식 원본과 동일한 바이트입니다. 배포할 때 이 폰트에 동봉된 저작권 및 OFL 라이선스를 함께 보존합니다.
