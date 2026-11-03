# 콘텐츠 업데이트 기록 — v1 → v2 (LocalServer)

SPEC 11장 "콘텐츠 업데이트 1회의 전후 기록". 절차는 SPEC 5.7을 따랐다.

## 조건

| 항목 | 값 |
|---|---|
| 날짜 | 2026-11-04 |
| 플레이어 | Windows 개발 빌드(`Builds/Windows/LobbyFlow.exe`). v1 전체 빌드 직후 만들어 v1 로컬 카탈로그를 담았다 |
| 서버 | 에디터 콘텐츠 서버(`http://localhost:8080/StandaloneWindows64`), 같은 PC |
| 기준 | `main`의 `addressables_content_state.bin`(v1 전체 빌드). 다시 한 전체 빌드도 커밋본과 같았다 |
| v2 콘텐츠 | `content/v2` 커밋의 `Assets/LobbyFlowContent/Remote/` 15개 파일을 `main` 작업 트리에 덮어 업데이트 빌드 |
| 실행 전 캐시 | v1 `patch_boot` 3개와 챕터 2 번들을 받은 상태. 챕터 3은 받은 적 없음 |
| 카탈로그 | JSON. `catalog_0.1.0.json` 38,625 → 39,500 바이트, 해시 변경 |

## 번들 변화

| 그룹 | 번들 | v1 | v2 | 결과 |
|---|---|---|---|---|
| Remote_Data | `remote_data_assets_all` | `a2c1bb8d…` 1,483 B | `bcde791b…` 1,547 B | 변경(코스튬 표 한 줄, 공지) |
| Remote_Season | `remote_season_assets_costumes` | `7b9aba53…` 59,914 B | `b9924dec…` 61,476 B | 변경(봄 축제 추가, 나이트 겨울 색) |
| Remote_Season | `remote_season_assets_event` | `dfd4e2f2…` 6,714 B | `83cd7c67…` 7,056 B | 변경(봄 배너) |
| Remote_Chapter02 | `remote_chapter02_scenes_all` | `7aa5edd8…` 2,840,484 B | `e88ce89e…` 2,840,740 B | 변경(수정 9개) |
| Remote_Chapter02 | `remote_chapter02_assets_all` | `e43be54c…` 55,232 B | `3ba338ef…` 55,227 B | 변경(머티리얼 색) |
| Remote_Chapter03 | `remote_chapter03_*` 2개 | `b711c15a…`, `d3bf3109…` | 같음 | 변경 없음 |
| Local_* | 로컬 번들 8개 | — | 같음 | 변경 없음(정적 그룹, 기준 파일 변화 없음) |

## 플레이어 결과 (`Player.log`, 콘텐츠 서버 로그)

| 구간 | 결과 |
|---|---|
| Boot | `카탈로그 변경 1개, 갱신한다` → `갱신 True, 받을 크기 0.1MB`. 받은 번들 3개(70,079 B): data, event, costumes |
| 데이터 | 코스튬 3 → 4 |
| 로비 | 봄 축제 배너와 공지. 챕터 2에 추가 다운로드 표시가 다시 켜짐 |
| 엠버 메이지 상세 | 봄 축제 코스튬, 나이트 겨울 코스튬 금색 |
| 챕터 2 | 바뀐 번들 2개(2,895,967 B)를 다시 받고 수정 9개 표시 |
| 챕터 3 | 번들은 바뀌지 않았다. 실행 전 캐시에 없어 처음 받았다(패치가 아닌 첫 다운로드) |

화면 확인은 사용자가 했고, 받은 번들과 크기는 로그로 확인했다.

## 알게 된 것

- 콘텐츠 업데이트 빌드는 로컬 카탈로그(`Library/com.unity.addressables/aa`)도 새로 쓴다. 업데이트 뒤 에디터의 Use Existing Build는 v2를 가진 앱처럼 동작하므로 v2 패치는 업데이트 전에 만든 플레이어로 확인한다.
- Addressables 2.8.1 업데이트 빌드에서 빌드 리포트 단계가 로컬 씬 번들 이름 중복으로 예외를 냈다. 번들과 카탈로그 결과와는 무관해 업데이트 빌드 동안만 리포트를 끈다.
- v2 빌더가 바뀌는 파일만 다시 만들어, 같은 값으로 다시 저장한 머티리얼과 챕터 3은 번들 해시가 그대로였다.
- 기록 뒤 빌드 리포트를 보니 챕터 씬 번들 약 2.8MB의 대부분은 원본 폰트(직렬화 4.69MB)의 복사본이었다. 폰트를 로컬 그룹으로 옮기는 변경(TMP 전환과 함께)을 한 뒤 챕터 다운로드 크기를 다시 기록한다.

## 후속: 폰트를 로컬 그룹으로 옮긴 뒤 (v1 전체 빌드 다시 함)

TextMeshPro 전환과 함께 동적 SDF 폰트 에셋을 `Local_Fonts` 그룹에 두었다. 빌드 리포트에서 원본 TTF는 `local_fonts` 번들 한 곳에만 들어간다.

| 번들 | 이전 | 이후 |
|---|---|---|
| `remote_chapter02_scenes_all` | 2,840,484 B | 23,453 B |
| `remote_chapter03_scenes_all` | 2,840,654 B | 23,643 B |
| `local_chapter01_scenes_all` | 2,840,241 B | 23,208 B |
| `local_fonts_assets_all` (앱 내장) | — | 2,876,396 B |

| 다운로드 | 이전 | 이후 |
|---|---|---|
| 챕터 2 진입 | 2,895,716 B | 78,685 B |
| 챕터 3 진입 | 2,961,648 B | 144,637 B |

Windows 개발 빌드는 166.2MB에서 168.2MB가 됐다. 빌드 씬과 UI 프리팹이 쓰는 폰트 에셋이 플레이어 데이터에도 한 벌 들어가기 때문이다. 폰트 에셋은 빌드 직전에 동적 데이터를 비워 아틀라스 1×1, 27KB 상태로 저장됐다.

## 후속 2: TMP 기준 v1 → v2 재검증

기준은 `main` `d824a18`(TMP 전환과 빌드 정리 뒤 v1), v2는 `content/v2` `b749cde`. 플레이어는 같은 기준으로 다시 만든 Windows 개발 빌드이고, 실행 전 v1 시작 패치와 챕터 2를 받아 둔 상태였다.

| 그룹 | 번들 | v1 | v2 |
|---|---|---|---|
| Remote_Data | `remote_data_assets_all` | `a2c1bb8d…` 1,483 B | `bcde791b…` 1,547 B |
| Remote_Season | `remote_season_assets_costumes` | `7b9aba53…` 59,914 B | `a3b7ed01…` 61,464 B |
| Remote_Season | `remote_season_assets_event` | `dfd4e2f2…` 6,714 B | `83cd7c67…` 7,056 B |
| Remote_Chapter02 | `remote_chapter02_scenes_all` | `43a991e8…` 23,453 B | `f658fb38…` 23,731 B |
| Remote_Chapter02 | `remote_chapter02_assets_all` | `e43be54c…` 55,232 B | `3ba338ef…` 55,227 B |
| Remote_Chapter03, Local_* (Local_Fonts 포함) | — | 같음 | 같음 |

| 구간 | 결과 |
|---|---|
| Boot | `갱신 True, 받을 크기 0.1MB`. 실제로 받은 것은 카탈로그(41,398 B)와 costumes(61,464 B) |
| 데이터 | 코스튬 3 → 4. 엠버 메이지 상세에서 봄 축제 코스튬 표시 |
| 챕터 2 | 실제로 받은 것은 `remote_chapter02_scenes_all`(23,731 B) |
| 챕터 3, 로컬 폰트 | 요청 없음 |
| 업데이트 빌드 | 빌드 리포트 예외 없음, 기준 파일 변화 없음 |

v2 변경 번들의 합은 시작 70,067 B, 챕터 2 78,958 B다. 실제로 받은 양이 이보다 적은 것은 data, event, chapter02 assets 번들이 앞선 v2 검증(TMP 전 기준) 때와 해시가 같아 캐시에 이미 있었기 때문이다. 내용이 같은 번들은 빌드를 다시 해도 이름(해시)이 같고, 캐시에 있으면 다시 받지 않는다.
