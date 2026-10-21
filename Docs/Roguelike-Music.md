# 테마 BGM

7개 테마의 오리지널 음악을 녹음된 기타·베이스·드럼 샘플 기반의 밴드 사운드로 편곡했습니다. 제작 방향은 록맨 X4·5·6에서 떠올린 멜로딕 액션 록이며, 원작의 멜로디나 음원 샘플은 사용하지 않았습니다. 녹음된 악기 단음을 편곡하고 앰프·공간 효과로 렌더링한 음원으로, 밴드의 실시간 합주 녹음은 아닙니다.

[전체 감상 페이지](../ArtSources/Music/listen.html) · [WAV 음원·샘플 출처·재생성 안내](../ArtSources/Music/README.md)

| 테마 | 곡명 | 길이 | BPM | 주요 분위기 |
| --- | --- | ---: | ---: | --- |
| 용암지대 | 용광 회로 / Molten Circuit | 1:41 | 152 | 팜뮤트 기타 리프와 상승하는 리드 기타 |
| 암석지대 | 수정 단층 / Crystal Fault | 1:47 | 144 | 하프타임 드럼, 묵직한 베이스, 클린 기타 |
| 정글지대 | 수관의 전류 / Canopy Voltage | 1:44 | 148 | 도리안 선율, 엇박 베이스와 타악기 |
| 빙결지대 | 프리즘 눈보라 / Prism Blizzard | 1:53 | 136 | 클린 기타 도입부, 리드 기타와 아르페지오 |
| 수상지대 | 조류의 신호 / Tidal Relay | 1:50 | 140 | 7화음, 움직이는 베이스와 엇박 기타 |
| 구름지대 | 천공의 벡터 / Skyward Vector | 1:38 | 156 | 장조 선율, 빠른 리듬과 열린 기타 |
| 폐허지대 | 매몰된 태양 기관 / Buried Sun Engine | 1:45 | 146 | 반음 긴장, 낮은 기타 리프와 깊은 탐 |

각 곡은 64마디, 44.1kHz 스테레오입니다. 도입·A·B·브레이크·솔로·재현·턴어라운드로 구성하고, 잔향을 처음으로 이어 반복 시 공백이 생기지 않게 했습니다.

`TowerGame.SpawnFloor()`에서 현재 테마를 `TowerMusic`에 전달합니다. 같은 테마의 층 사이에서는 재생 위치가 유지됩니다. 테마가 바뀌면 두 AudioSource를 1.25초 동안 크로스페이드하고 이전 소스를 정지합니다. 배경음악은 효과음과 별도 소스를 사용하며 기본 볼륨은 0.18입니다.

게임용 파일은 `Assets/Demos/Roguelike/Resources/Tower/Music/{lava,rock,jungle,frozen,water,cloud,ruins}.ogg`입니다. `TowerMusicImport`가 스테레오 스트리밍·Vorbis·품질 0.85·샘플레이트 유지·백그라운드 로드 설정을 적용합니다.

파일 검증에서는 7곡의 90~120초 길이, 스테레오, 클리핑 없음, 루프 경계와 무음 여부를 확인합니다. 음원 수치와 해시는 `ArtSources/Music/manifest.json`에 있습니다. 직접 청음 평가를 수행했다는 뜻은 아닙니다. 샘플 출처와 라이선스는 `ArtSources/Music/Samples/{Guitar,Bass,Drums}/manifest.json`에 기록하고 종합 manifest에도 포함합니다.

Unity 검증은 `Tests/Run-TowerMusicSmoke.ps1`로 실행합니다. 실제 재생 시간 진행, 1→2층의 음악 유지, 5→6층의 크로스페이드, 7개 테마 매핑, 루프, 중지·재개, 소스 정리와 효과음 독립성을 검사합니다. 층 전환은 검증용 fixture이며 전투 전체를 플레이한 결과가 아닙니다.

2026-10-20 밴드 음원으로 교체한 뒤 에디터 검증에서 7곡과 9개 동작 항목을 모두 통과했고 오류는 0개였습니다. 결과는 `Artifacts/TowerEditor/Music/music-20261020-171834-report.json`입니다. 검증 후 기존 씬의 열린 상태·활성 상태·저장되지 않은 변경 여부가 동일하고 실행 중지 상태인 것을 확인했습니다.

밴드 음원이 포함된 Windows 실행 파일은 `Build/TowerBand/TowerBand.exe`입니다. 공유할 때는 `TowerBand` 폴더 전체를 전달합니다. `Music-Licenses/`에는 샘플 라이브러리의 출처와 CC0 라이선스 자료를 함께 넣었습니다.

Windows 실행 파일에서도 7곡·9개 동작 검사를 통과했고 종료 코드는 0입니다. 첫 실행은 층 전환 직후의 크로스페이드 타이밍 검사에서 실패했고, 같은 파일과 옵션으로 한 번 재실행하여 통과했습니다. 페이드가 실제 프레임 시간에 의존하므로 시작 시 프레임 지연의 영향이 의심되지만, 원인을 확정한 결과는 아닙니다. 첫 보고서·로그는 `Artifacts/TowerEditor/Music/band-player-first-attempt-report.json`과 `band-player-first-attempt.log`에 보존했고, 통과 보고서는 `band-player-retry-report.json`에 있습니다. 음원이나 게임 재생 코드는 재실행 사이에 변경하지 않았습니다.

빌드 보고서는 `Artifacts/TowerEditor/Music/band-build-report.json`입니다. 빌드 전후 모든 ProjectSettings 파일의 해시와 메모리의 플레이어·셰이더 설정을 비교했고, 프로젝트 설정의 원래 바이트와 열린 씬 상태를 복원했습니다.
