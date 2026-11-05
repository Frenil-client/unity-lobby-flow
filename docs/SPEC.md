# unity-lobby-flow SPEC

작성일: 2026-11-03 · 기준 계획: `비동기_로딩_로비_프로젝트_계획_2026-11-02.md`

이 문서는 구현 범위, 동작 규칙, 리소스 배포와 CI 구성을 정의한다.
계획서의 목적과 검증 시나리오는 그대로 유지하고, 원격 Addressables·패치·Jenkins 자동화를 범위에 추가한다.

---

## 1. 목표

- 비동기 로딩의 예외 처리, 리소스 수명, 플레이어 빌드 검증을 보여 주는 작은 로비 데모를 만든다.
- 리소스는 Addressables로 관리한다. 로컬(앱 내장)과 원격(Unity CCD) 그룹을 기준에 따라 나누고, 원격 콘텐츠를 실제로 내려받는다.
- 게임 시작 시 원격 카탈로그 변경을 확인하고, 패치 팝업에서 진행률을 보여 주며 내려받는 시퀀스를 만든다.
- 로드한 리소스는 소유자가 반드시 `Addressables.Release`로 반환하고, Addressables Profiler로 늘었다 줄어드는 과정을 기록한다.
- Android와 iOS 빌드, Addressables 갱신, 리소스 무결성 검사, 기능 테스트를 Jenkins 파이프라인으로 재현 가능하게 만든다.

### 계획서 대비 범위 변경

| 항목 | 계획서 | 변경 |
|---|---|---|
| 원격 다운로드 | 제외 | **포함**. 저장소는 Unity CCD |
| 패치 시스템 | 제외 | **포함**. 시작 시 카탈로그 확인, 패치 팝업, 진행률, 챕터 진입 시 추가 다운로드, 콘텐츠 업데이트 빌드 |
| 메모리 계측 | 로딩 지연·메모리 관측 | Addressables Profiler 캡처·영상과 Memory Profiler 스냅샷 비교를 산출물로 추가 |
| 빌드 대상 | Android 우선, 안 되면 PC | Android, iOS |
| CI | 없음 | Jenkins 파이프라인 |
| 실제 서버, 로그인, 결제 | 제외 | 제외 유지 |

---

## 2. 환경

| 항목 | 값 |
|---|---|
| Unity | 6000.3.9f1, URP |
| 직렬화 | Force Text, Visible Meta Files |
| UI | `com.frenil.uisystem` v0.3.1 (git URL, `Packages/manifest.json`), 글자는 TextMeshPro(`com.unity.ugui` 2.0) |
| 리소스 | Addressables 2.8.1 (`com.unity.addressables`) |
| 원격 저장소 | Unity Cloud Content Delivery(CCD). Unity Cloud 프로젝트 연결 필요 |
| 개발 PC | Windows 11 |
| Android 검증 | 보유 Android 기기 |
| iOS 검증 | 보유 Mac(Jenkins macOS 노드 겸용) + Xcode |

---

## 3. 기능 범위

| 기능 | 상태 |
|---|---|
| 로비: 캐릭터 목록, 상세 팝업, 3D 미리보기, 좌우 전환 | 구현됨(임시 직접 로더) |
| 던전 씬 왕복, 로딩 오버레이 | 구현됨 |
| 로딩·실패·재시도 표시, 지연·실패 주입 테스트 메뉴(개발 빌드) | 구현됨 |
| Boot 씬: 카탈로그 확인 → 패치 팝업 → 다운로드 → 로비 | 화면 흐름 구현(모의 패치 서비스). 실제 연결은 Addressables 단계 |
| 오류 팝업(재시도/종료), 오프라인 안내 토스트 | 구현 |
| 챕터 선택(1/2/3)과 챕터 씬 3개 | 구현. 챕터 2·3 원격 전환과 진입 다운로드는 Addressables 단계 |
| 시즌 이벤트 코스튬: 상세 팝업에서 기본/코스튬 전환, 던전에 반영 | 구현. 원격 전환은 Addressables 단계 |
| 캐릭터 초상·챕터 아이콘, 버튼음·로비 BGM | 구현. 빌더가 직접 그리고 합성한다(외부 라이선스 없음) |
| 테스트 메뉴: 모의 패치 조건, 다운로드 끊김, 캐시 삭제, Boot 다시 실행 | 구현 |
| 테스트 메뉴: Addressables 집계 표시 | 계획 |

UI는 `com.frenil.uisystem`의 `UIScreen`/`UIPopup`과 `OpenAsync<T>()`를 사용한다.
패키지에 없는 기능이 필요하면 이 저장소에서 구현하고, 패키지 수정이 필요한지는 따로 판단한다.

---

## 4. 동작 규칙

### 4.1 검증 시나리오

| 상황 | 기대 동작 |
|---|---|
| 캐릭터 로딩 중 팝업 닫기 | 완료된 결과가 닫힌 화면에 생성되지 않고 리소스가 정리된다 |
| A 선택 직후 B 선택, A가 늦게 완료 | 마지막으로 선택한 B가 표시되고 A 결과는 반영되지 않는다 |
| 로딩 중 씬 이탈 후 재진입 | 이전 화면의 요청이 새 화면을 변경하지 않는다 |
| 로드 실패 후 재시도 | 오류 상태와 재시도 경로가 보이고 정상 상태로 복구된다 |
| 팝업 열고 닫기, 씬 왕복 반복 | 중복 생성, 해제 누락, 반복 횟수에 비례한 지속 누적이 없다 |
| 시작 시 카탈로그 변경 있음 | 패치 팝업에 받을 크기가 표시되고, 받은 뒤 변경된 리소스가 표시된다 |
| 시작 시 카탈로그 변경 없음 | 팝업 없이 로비로 들어간다 |
| 패치 다운로드 중 네트워크 끊김 | 실패 상태와 재시도 경로가 보이고, 재연결 후 재시도로 복구된다. 이미 받은 번들은 다시 받지 않는다 |
| 오프라인으로 시작(이전 실행 기록 있음) | 경고를 보이고 캐시된 콘텐츠로 로비에 들어간다 |
| 오프라인으로 첫 실행 | 오류 팝업과 재시도만 보이고 로비로 가지 않는다 |
| 챕터 2 첫 진입 / 재진입 | 첫 진입은 다운로드 팝업, 재진입은 캐시에서 바로 로드 |
| 콘텐츠 업데이트 후 재실행 | 바뀐 번들만 내려받는다(받을 크기로 확인) |

### 4.2 설계 규칙

- 화면의 수명과 로드 요청의 수명을 구분한다. 요청마다 식별자를 발급하고, 완료 시점에 현재 유효한 요청인지 판별한다.
- 취소 요청과 실제 로드 작업의 종료를 구분한다. Addressables 핸들은 중간 취소가 없으므로 완료를 기다린 뒤 소유권 규칙에 따라 반환한다.
- Addressables 핸들과 생성 인스턴스는 소유자를 하나로 정한다. 소유자만 반환하며 이중 해제와 해제 누락을 함께 점검한다([7장](#7-메모리-관리와-프로파일링)).
- 카탈로그 갱신은 Boot 씬에서 원격 리소스를 하나도 로드하지 않은 상태에서만 한다. 게임 도중에는 갱신하지 않는다.
- 패치 UI와 Boot 흐름은 원격 콘텐츠에 의존하지 않는다. 앱에 내장된 리소스만 쓴다.
- 화면 종료 시 반환할 리소스와 유지할 캐시를 정한다. 프로세스 메모리가 초기값으로 즉시 돌아오는 것을 완료 기준으로 삼지 않는다.
- 실제 로더와 테스트용 지연·실패 주입을 인터페이스로 분리한다. **모의 실패 검증과 실제 네트워크 장애 검증은 결과 기록에서 구분해 적는다.**

---

## 5. Addressables 구성

### 5.1 로컬·원격 분리 기준

| 기준 | 로컬(앱 내장) | 원격(CCD) |
|---|---|---|
| 필요한 시점 | 첫 실행, 패치 전, 오프라인에서도 필요 | 특정 챕터·이벤트에 들어갈 때 필요 |
| 변경 주기 | 앱 업데이트 때만 바뀐다 | 앱 업데이트 없이 바꾸거나 추가한다 |
| 없을 때 결과 | 게임을 시작할 수 없다 | 해당 콘텐츠만 막히고 나머지는 동작한다 |
| 업데이트 설정 | 출시 후 변경 금지(Prevent Updates) | 출시 후 변경 허용 |

판단이 애매하면 "이게 없으면 로비까지 갈 수 있나"로 정한다. 갈 수 없으면 로컬이다.
로컬 그룹의 에셋을 출시 후 바꾸면 콘텐츠 업데이트 제한 검사에 걸린다. 그런 변경은 앱 업데이트로 처리하고, 원격으로 옮기지 않는다.

### 5.2 폴더와 그룹

Addressables 콘텐츠는 `Assets/LobbyFlowContent/` 한 곳에 두고, 폴더가 곧 그룹 경계다.

```
Assets/LobbyFlowContent/
├─ Local/                          앱 내장. 출시 후 변경 금지
│  ├─ UI/              (2D)        캐릭터 초상, 챕터 아이콘
│  ├─ Audio/                       로비 BGM
│  ├─ Fonts/                       한글 TMP 동적 폰트 에셋(NanumGothic SDF)
│  ├─ Characters/      (3D)
│  │  ├─ Common/                   캐릭터 공용 머티리얼(피부)
│  │  └─ knight/ ranger/ mage/     기본 캐릭터 프리팹·머티리얼
│  ├─ Chapter01/                   챕터 1 씬과 전용 머티리얼
│  └─ Data/            (데이터)    characters.csv, chapters.csv
└─ Remote/                         CCD. 패치 대상. content/v2 브랜치는 이 아래만 바꾼다
   ├─ Chapter02/                   챕터 2 씬과 전용 머티리얼
   ├─ Chapter03/                   챕터 3 씬과 전용 머티리얼
   ├─ Season/
   │  ├─ Costumes/     (3D)        시즌 코스튬 프리팹·머티리얼
   │  └─ Event/        (2D)        이벤트 배너
   └─ Data/            (데이터)    costumes.csv, event_notice.txt
```

| 그룹 | 폴더 항목(주소) | 묶음 방식 |
|---|---|---|
| Local_UI | `Local/UI` (`UI`) | 함께 |
| Local_Audio | `Local/Audio` (`Audio`) | 함께 |
| Local_Fonts | `Local/Fonts` (`Fonts`) | 함께 |
| Local_Characters | `Characters/Common`, `Characters/knight` … 캐릭터마다 하나 | 항목마다 |
| Local_Chapter01 | `Local/Chapter01` (`Chapter01`) | 함께 |
| Local_Data | `Local/Data` (`LocalData`) | 함께 |
| Remote_Chapter02 | `Remote/Chapter02` (`Chapter02`) | 함께 |
| Remote_Chapter03 | `Remote/Chapter03` (`Chapter03`) | 함께 |
| Remote_Season | `Remote/Season/Costumes` (`Costumes`), `Remote/Season/Event` (`Event`) | 항목마다 |
| Remote_Data | `Remote/Data` (`RemoteData`) | 함께 |

- 그룹에는 폴더 단위로 등록한다. 안의 파일은 `Characters/knight/Character_knight.prefab`처럼 폴더 주소 아래 상대 경로가 주소가 된다. v2에서 폴더 안에 파일이 늘어나도 그룹 설정 파일은 바뀌지 않는다.
- 폴더 항목 하나는 번들 하나다. "항목마다"로 묶어도 폴더 안 파일은 같은 번들에 들어가므로, 따로 받거나 내릴 단위는 하위 폴더로 나눠 각각 등록한다. 원격의 하위 폴더 구성은 v1에서 정하고 v2는 기존 폴더 안에서만 파일을 바꾸거나 더한다.
- Boot 씬, 패치 팝업, 로딩 오버레이, UI 시스템 설정, 버튼음은 Addressables가 아니라 빌드 씬과 `Resources`(또는 그것이 직접 참조하는 에셋)에 둔다. 카탈로그를 받기 전에 필요하기 때문이다.
- 로비 씬도 빌드 씬으로 둔다. 로비가 쓰는 리소스(초상, BGM, 배너)는 Addressables로 로드하고 로비 화면이 반환한다. 챕터 씬은 Addressables 씬이며 빌드 설정에 넣지 않는다.
- 번들 안 일부만 반환해도 번들 전체가 내려가지 않으므로([7.3](#73-번들-단위-메모리)) 수명이 다른 것은 같은 번들에 넣지 않는다. 코스튬은 한 번들이라 하나만 써도 코스튬 번들 전체가 올라온다(데모 규모에서 허용).
- 폰트는 로컬 그룹(`Local_Fonts`)에 둔다. Addressables가 아닌 폰트를 여러 씬 번들이 참조하면 번들마다 복사본이 들어간다. 실제로 v1에서 원본 TTF(직렬화 4.69MB)가 챕터 1·2·3 씬 번들에 각각 들어가, LZ4 압축 후 씬 번들 하나가 약 2.8MB로 챕터 다운로드 크기 대부분을 차지했다(빌드 리포트로 확인). 빌드 씬과 UI 프리팹도 같은 폰트를 쓰므로 플레이어 데이터와 로컬 번들에는 한 벌씩 들어간다(둘 다 앱 내장, 다운로드 없음).
- 폰트 에셋은 동적 모드다. 실행 중 쓰는 글자만 아틀라스에 넣는다. 디스크에는 항상 비운 상태로 저장하고(저장 직전 보정, `DynamicFontSaveGuard`), Addressables 빌드 메뉴와 플레이어 빌드 직전에도 비운다. 비우지 않으면 에디터 플레이로 쌓인 글자 때문에 로컬 폰트 번들 해시가 바뀌어 콘텐츠 업데이트가 정적 그룹 변경으로 본다.
- 압축은 LZ4로 시작한다. 원격 번들의 다운로드 크기를 줄일 필요가 생기면 LZMA를 비교하고 결과를 기록한다.
- 그룹과 라벨은 구현하면서 조정하고, 바뀌면 이 표를 갱신한다.

### 5.3 라벨

| 라벨 | 대상 | 쓰임 |
|---|---|---|
| `patch_boot` | `Costumes`, `Event`, `RemoteData` | Boot 패치 다운로드 대상 |
| `chapter02`, `chapter03` | `Remote/Chapter02`, `Remote/Chapter03` | 챕터 진입 시 다운로드 대상 |

라벨은 폴더 항목에 붙여 안의 파일 전체에 적용한다. 코스튬 목록은 라벨이 아니라 `costumes.csv`로 조회한다.

### 5.4 프로필과 경로

| 프로필 | RemoteLoadPath | 용도 |
|---|---|---|
| Local | 로컬 빌드 폴더(에디터 호스팅 또는 Use Existing Build) | 에디터와 오프라인 검증 |
| LocalServer | `http://[ServerHost]:8080/[BuildTarget]`. 빌드 창의 **콘텐츠 서버**가 `ServerData/`를 내보낸다. 켜 둔 서버는 Play 진입 등 스크립트를 다시 읽은 뒤 자동으로 다시 연다 | CCD 연결 전 패치 과정 검증. `ServerHost`는 `localhost`로 둔다. Android 기기는 USB로 연결하고 빌드 창의 **Android 기기 연결 (adb reverse)**로 기기의 localhost:8080을 PC로 잇는다(**Android 개발 빌드 후 기기 실행**은 LocalServer 프로필일 때 이 연결을 먼저 한다). CCD 연결 때 원격 경로를 CCD로 바꾼다 |
| CCD | `https://{CcdConfig.ProjectId}.client-api.unity3dusercontent.com/client_api/v1/environments/{CcdConfig.Environment}/buckets/{CcdConfig.BucketId}/release_by_badge/{CcdConfig.Badge}/entry_by_path/content/?path=`(`LobbyFlow.Patching.CcdConfig`를 실행 중에 읽는다) | 실기기와 출시 빌드. 빌드 창의 **프로필**로 고르고, 바꾼 뒤에는 전체 빌드와 플레이어 빌드를 다시 한다 |

- CCD는 production 환경 하나, 플랫폼별 공개 버킷(`lobby-flow-windows`, `lobby-flow-android`, `lobby-flow-ios`, 2026-11-04 생성), `latest` 배지를 쓴다. 공개 여부는 생성 뒤 바꿀 수 없고, Addressables가 인증 없이 받으려면 공개여야 한다. 버킷 ID는 `CcdConfig`가 플랫폼 심볼(`UNITY_ANDROID`, `UNITY_IOS`)로 고른다. 프로젝트·버킷 ID는 공개 다운로드 주소의 일부라 저장소에 둔다. 리플렉션으로 읽으므로 `[Preserve]`로 코드 제거를 막는다.
- 콘텐츠를 올릴 때마다 릴리스를 만들고, CCD가 새 릴리스에 `latest` 배지를 옮긴다(2026-11-05 릴리스 1에서 확인). 콘텐츠 버전 릴리스에는 `v1`, `v2` 배지를 붙인다(업로드 때 빌드 창에서 고르거나 `ugs ccd badges create`).
- 배지 선택: 출시 빌드는 항상 `latest`. 개발 빌드는 테스트 메뉴 **CCD 콘텐츠**에서 `v1 → v2 → latest`를 고르고(처음 `v1`, 기기에 저장), 앱을 다시 시작하면 그 배지로 카탈로그를 확인한다. Addressables가 시작할 때 주소를 한 번 읽기 때문에 실행 중에는 바꿀 수 없다. 설치한 기기에서 겨울(v1) → 봄(v2) 패치를 보기 위한 것이다.
- 공개 저장소 정책: CCD 다운로드는 누구나, 업로드는 작성자만(서비스 계정 키는 작성자 PC 환경 변수에만 있다). 받은 사람의 버전 변경 테스트는 LocalServer로 한다(`docs/guides/local-patch-test.md`). 빌드 창은 키가 없으면 업로드를 숨기고, 키 없이 배지별 카탈로그 해시를 받아 보는 "CCD 연결 확인"을 준다. 플레이어 버전이 바뀌어 호환이 깨지면 배지를 따로 만든다(예: `player-1`).
- `Assets/AddressableAssetsData/<Platform>/addressables_content_state.bin`은 콘텐츠 업데이트 빌드에 필요하므로 커밋한다.
- `ServerData/`는 빌드 산출물이며 저장소에 올리지 않는다.

### 5.5 원격 저장소

**선택: Unity CCD**

| 후보 | 무료 범위 | 판단 |
|---|---|---|
| **Unity CCD** | 월 대역폭 50GB | 채택. Addressables 프로필에 버킷·배지를 바로 연결할 수 있고, 릴리스·배지로 콘텐츠 버전을 관리한다 |
| Cloudflare R2 | 저장 10GB, 다운로드 트래픽 무료 | 대안. 일반 HTTP 경로라 프로필 경로와 업로드 단계만 바꾸면 옮길 수 있다 |

- 요금(2026-11-04 UGS 가격 페이지): 대역폭 월 50GB 무료, 초과 시 GB당 $0.08. 저장 용량은 별도 상한이 적혀 있지 않다. 리뷰 기사 기준으로 결제 카드 등록이 필요하다고 하며, 대시보드에서 확인한다.
- 업로드: UGS CLI 2.0(`ugs`)의 `ugs ccd entries sync ServerData/<BuildTarget> -p <프로젝트> -e production -b <버킷> -r -n <메모>` 한 번으로 올리고 릴리스를 만든다. 지우기(`-x`)는 쓰지 않아 이전 카탈로그의 번들이 버킷에 남는다. 빌드 창의 **현재 플랫폼 업로드와 릴리스**가 같은 명령을 부르고, CCD 프로필이 아니거나 ID가 비어 있으면 거부한다.
- 인증: 서비스 계정(조직 역할 Cloud Content Delivery API Public Editor, 프로젝트 역할 Unity Environments Viewer). 키는 사용자 환경 변수 `UGS_CLI_SERVICE_KEY_ID`, `UGS_CLI_SERVICE_SECRET_KEY` 또는 `ugs login`으로 주고, Jenkins에서는 Credentials로 넣는다.
- 반복 검증 중 캐시를 지우고 다시 받는 횟수가 대역폭을 쓴다. 데모 콘텐츠를 수십 MB 안으로 유지하고, 측정 기록에 받은 총량을 적는다.
- 업로드용 키는 Jenkins Credentials에만 두고 저장소와 빌드에 넣지 않는다.

### 5.6 데이터 테이블

게임 데이터는 CSV와 텍스트로 두고 Addressables로 `TextAsset`을 로드해 파싱한다. 리소스는 데이터 안의 주소 문자열로 가리킨다.
코스튬처럼 원격 데이터가 원격 리소스를 가리키면, 앱 업데이트 없이 데이터 한 줄과 리소스 하나로 콘텐츠를 추가할 수 있다.

| 파일 | 위치 | 열 |
|---|---|---|
| `characters.csv` | 로컬 | `id, name, role, description, color, portrait, prefab` |
| `chapters.csv` | 로컬 | `id, name, scene, icon, download_label` |
| `costumes.csv` | 원격 | `id, character_id, name, seasonal, prefab` |
| `event_notice.txt` | 원격 | 첫 줄 제목, 나머지 본문 |

- 형식: UTF-8, 첫 줄 머리글, 쉼표 구분. 쉼표·줄바꿈·따옴표가 든 값은 큰따옴표로 감싸고 따옴표는 두 번 쓴다. 빈 줄과 `#`로 시작하는 줄은 건너뛴다.
- 색은 `#RRGGBB`, 참·거짓은 `true`/`false`. 비어 있는 `download_label`은 받을 것이 없는 로컬 챕터다.
- 파싱 오류(머리글 누락, 중복 id, 없는 캐릭터를 가리키는 코스튬)는 Boot에서 오류로 처리하고 로비로 가지 않는다.
- 로컬 데이터는 원격 주소를 가리킬 수 있다(챕터 2·3 씬). 이 주소는 출시 후 바꾸지 않는다. 바꾸려면 앱 업데이트다.
- 데이터 테이블은 Boot 패치가 끝난 뒤 한 번 읽어 앱이 켜져 있는 동안 유지한다. 파싱 후 `TextAsset` 핸들은 바로 반환한다.

### 5.7 콘텐츠 버전 관리 (브랜치)

| 브랜치 | 내용 |
|---|---|
| `main` | 코드 전체와 콘텐츠 v1. 저장소를 받으면 v1이다 |
| `content/v2` | `main`에서 갈라져 `Assets/LobbyFlowContent/Remote/` 아래만 바꾼 커밋 |

| 항목 | v1 | v2 |
|---|---|---|
| 시즌 코스튬 | 겨울 축제 3종 | 나이트 겨울 축제 색 변경, 봄 축제 1종 추가 |
| `costumes.csv` | 3줄 | 4줄 |
| 이벤트 공지·배너 | 겨울 축제 | 봄 축제 |
| 챕터 2 | 수정 6개 | 배치와 색 변경 |
| 로컬 콘텐츠 | — | 바꾸지 않는다 |

패치 테스트 절차:

빌드, 프로필, 콘텐츠 서버, CCD 업로드는 **Lobby Flow ▸ 빌드 창** 한 곳에서 한다. 창은 현재 플랫폼, 프로필, 이 플랫폼 Addressables 빌드가 현재 프로필과 맞는지, 업데이트 기준 유무를 보여 주고, 맞지 않으면 플레이어 빌드와 CCD 업로드 버튼을 막는다. 기능은 `AddressablesBuild`, `PlayerBuild`, `ContentServer`, `CcdUpload` 정적 메서드에 있고 Jenkins도 같은 메서드를 부른다.

1. `main`에서 빌드 창 **전체 빌드** → 원격 번들 업로드(LocalServer는 **콘텐츠 서버 시작**) → **Windows 개발 빌드**. 생긴 `addressables_content_state.bin`을 `main`에 커밋한다.
2. `git restore --source=content/v2 -- Assets/LobbyFlowContent/Remote`로 원격 폴더만 v2로 덮는다. 브랜치는 바꾸지 않는다.
3. 빌드 창 **콘텐츠 업데이트 빌드**(1번 기준 사용) → 업로드.
4. 1번 플레이어를 켜서 카탈로그 갱신, 패치 팝업의 받을 크기, 바뀐 코스튬·공지·챕터 2를 확인한다. 콘텐츠 서버 로그로 받은 번들을 확인한다.
5. `git restore -- Assets/LobbyFlowContent/Remote`와 `git clean -fd -- Assets/LobbyFlowContent/Remote`로 v1으로 되돌린다.

v2 패치는 1번 플레이어로 검증한다. 콘텐츠 업데이트 빌드는 원격 카탈로그와 함께 로컬 카탈로그(`Library/com.unity.addressables/aa`)도 새로 쓰므로, 업데이트 뒤 에디터의 **Use Existing Build**는 이미 v2를 가진 앱처럼 동작해 카탈로그 갱신 단계가 일어나지 않는다. 같은 이유로 업데이트 뒤에 만든 플레이어는 출시본(v1)을 대신하지 못한다.
에디터에서 v1 패치(첫 설치 다운로드)를 볼 때는 Play Mode Script를 **Use Existing Build**로 둔다. 기본값(Use Asset Database)은 빌드 결과와 원격 경로를 쓰지 않아 패치가 일어나지 않는다.
플레이어 빌드는 Addressables를 다시 빌드하지 않는다(`Build Addressables on Player Build` 끔). 콘텐츠는 빌드 메뉴로만 만들어 업데이트 기준이 몰래 바뀌지 않게 한다. 빌드 동안만 바뀌는 `preloadedAssets`(Input System)와 `AddressableAssetsData/link.xml`(Addressables)은 빌드 메뉴가 빌드 직후 디스크에서도 되돌린다. `link.xml`은 추적하지 않는다. LocalServer는 http라 `Allow downloads over HTTP`는 개발 빌드에서만 허용한다.
`content/v2`의 내용은 **Lobby Flow ▸ 원격 콘텐츠 v2 생성**으로 만든다. v1 대비 바뀌는 파일만 다시 만들며, 나머지 파일은 그대로 두어 불필요한 번들 변경이 생기지 않게 한다.

규칙:

- `content/v2`는 `Remote/` 밖을 바꾸지 않는다. `git diff --stat main...content/v2 -- . ':!Assets/LobbyFlowContent/Remote'`가 비어 있어야 한다(무결성 검사 항목).
- 기존 에셋을 바꿀 때는 `.meta`를 유지해 GUID가 그대로여야 한다. 새 에셋은 v2 브랜치에 `.meta`와 함께 커밋한다.
- `main`에서 `Remote/`를 바꾸면 `content/v2`를 `main` 위로 다시 올린다(rebase).
- v2 콘텐츠는 빌더 메뉴로 만든다. 브랜치 생성·커밋·push는 사용자가 한다.

---

## 6. 카탈로그 업데이트와 패치

### 6.1 다운로드 시점

| 시점 | 대상 | 이유 |
|---|---|---|
| 게임 시작(Boot) | 바뀐 카탈로그, `patch_boot` 라벨 | 시즌 이벤트처럼 로비에서 바로 보이는 원격 콘텐츠 |
| 챕터 진입 | `chapter02` / `chapter03` 라벨 | 들어가지 않는 챕터는 받지 않는다 |

로비의 챕터 버튼은 라벨이 있는 챕터마다 남은 받을 크기를 확인해, 0보다 클 때만 "추가 다운로드" 표시를 켠다. 확인하는 동안은 끄고, 확인에 실패하면 받아야 하는 것으로 보고 켠다. 챕터 다운로드를 마치면 바로 끈다. v2 업데이트로 챕터 번들이 바뀌면 다시 켜진다.

### 6.2 Boot 시퀀스

```
Boot 씬 (빌드 내장)
 ├─ Addressables 초기화
 ├─ 카탈로그 변경 확인 ── 원격 해시 파일만 받아 캐시된 카탈로그와 비교한다
 │   └─ 변경 있음 → 카탈로그 갱신
 ├─ patch_boot 다운로드 크기 계산
 │   ├─ 0 바이트 → 로비로
 │   └─ 0 초과 → 패치 팝업(받을 크기, [다운로드] [종료])
 │        └─ 다운로드 → 진행률(%, 받은 MB / 전체 MB) → 완료 → 핸들 반환 → 로비로
 └─ 각 단계 실패 → 오류 팝업([재시도] [종료]) 또는 6.4 정책
```

- 시작 시 자동 카탈로그 갱신은 끄고(설정의 수동 갱신 옵션) 이 시퀀스에서만 갱신한다.
- 카탈로그 형식: JSON(`EnableJsonCatalog`와 스크립트 심볼 `ENABLE_JSON_CATALOG`, Standalone·Android·iOS). 설정 값만 켜면 빌드는 여전히 바이너리(`catalog_*.bin`)를 만든다. Addressables 2.x 기본값은 바이너리지만 패치 결과를 사람이 읽고 비교할 수 있게 JSON을 쓴다. 변경 확인은 형식과 무관하게 해시 파일 비교로 이루어진다.
- 다운로드 핸들은 완료 후 반환한다. 받은 번들은 캐시에 남고, 핸들 반환은 캐시를 지우지 않는다.

### 6.3 패치 팝업

- 내장 UI(`Resources`의 프리팹 표)로 만든다. `UIPopup`, 뒤로가기로 닫히지 않는다.
- 표시: 받을 크기, 진행 막대, 퍼센트, 받은 크기/전체 크기, 단계 문구(확인 중 / 받는 중 / 완료).
- 진행률은 다운로드 상태의 받은 바이트 기준으로 계산한다. 작업 진행률은 단계 비중이 섞여 막대가 튈 수 있다.
- 모바일 데이터 연결이면 크기와 함께 안내 문구를 붙인다(차단하지 않는다).

### 6.4 실패와 예외 처리

| 상황 | 처리 |
|---|---|
| 카탈로그 확인 실패, 이전에 받은 카탈로그와 콘텐츠가 있음 | 경고 토스트를 띄우고 캐시된 콘텐츠로 로비 진입 |
| 카탈로그 확인 실패, 캐시가 없음(첫 실행) | 오류 팝업, 재시도 |
| 다운로드 중 실패 | 오류 팝업, 재시도. 끝까지 받은 번들은 캐시에 남으므로 이어서 받는다 |
| 다운로드 중 앱이 백그라운드로 감 | 복귀 후 진행 상태를 확인하고 실패했으면 재시도 경로로 |
| 챕터 다운로드 실패 | 챕터 진입만 취소하고 로비에 남는다 |

**정책: 첫 실행이 아니면 오프라인 진입을 허용한다.** 캐시된 카탈로그와 이미 받은 콘텐츠로 로비에 들어가고, 받지 않은 챕터는 진입 시 다운로드 실패로 처리한다.
캐시된 카탈로그가 없는 첫 실행만 차단하고 재시도를 요구한다.

### 6.5 콘텐츠 업데이트 절차

1. 원격 그룹의 에셋을 바꾼다(예: 코스튬 색 변경, 챕터 2 배치 변경).
2. 이전 빌드의 `addressables_content_state.bin`으로 콘텐츠 업데이트 빌드를 한다.
3. CCD에 올리고 릴리스를 만든 뒤 배지를 옮긴다.
4. 앱을 다시 켜서 패치 팝업의 받을 크기가 바뀐 번들 크기와 맞는지, 바뀐 내용이 보이는지 확인한다.

### 6.6 검증 도구

- 테스트 메뉴에 다운로드 캐시 삭제를 둔다. 첫 실행 상태를 반복 재현하는 데 쓴다.
- 네트워크 끊김은 기기의 비행기 모드로 실제로 만든다. 지연 주입 결과와 섞어 적지 않는다.

---

## 7. 메모리 관리와 프로파일링

### 7.1 핸들 소유 규칙

- `LoadAssetAsync`로 받은 핸들은 그 리소스를 쓰는 화면 하나가 소유한다. 소유 객체(`PreviewAsset` 등)가 핸들을 들고, 반환은 `Release()` 한 곳에서만 한다.
- 인스턴스는 받은 프리팹으로 `Object.Instantiate` 하고 `Object.Destroy`로 지운다. 인스턴스를 모두 지운 뒤 핸들을 반환한다. `InstantiateAsync`는 쓰지 않아 반환 경로를 하나로 유지한다.
- 같은 키를 여러 화면이 로드하면 각자 핸들을 받고 각자 반환한다. 참조 수가 0이 될 때 내려간다.
- 챕터 씬은 `ReleaseSceneWhenSceneUnloaded`(기본값)로 로드한다. 다음 씬으로 넘어가 챕터 씬이 언로드되면 Addressables가 핸들을 반환하므로 `SceneFlow`는 핸들을 들고 있지 않는다. 로드에 실패한 핸들만 직접 반환한다.
- 다운로드 핸들(`DownloadDependenciesAsync`)은 완료나 실패 직후 반환한다.

### 7.2 해제 시점

| 리소스 | 소유자 | 반환 시점 |
|---|---|---|
| 상세 팝업 미리보기 | `CharacterPreviewPresenter`(팝업) | 캐릭터 전환, 팝업 닫힘, 씬 언로드 |
| 던전 캐릭터 | `CharacterPreviewPresenter`(던전 화면) | 던전 화면 파괴 |
| 로비 초상·UI 이미지 | 로비 화면 | 로비 화면 파괴 |
| 로비 BGM | 로비 화면 | 로비 화면 파괴 |
| 챕터 씬 | Addressables(씬 언로드 시 자동) | 다음 씬 전환 |
| 패치 다운로드 | Boot 시퀀스 | 다운로드 완료·실패 직후 |

캐시: 처음에는 화면이 닫히면 모두 반환한다. 반복 로드 비용이 측정으로 확인되면 유지할 캐시를 정하고 이 표에 적는다.

### 7.3 번들 단위 메모리

- 에셋 하나를 반환해도, 같은 번들의 다른 에셋을 누가 쓰고 있으면 번들은 메모리에 남는다. Profiler에서 에셋 수는 줄었는데 번들이 남아 있으면 묶음 방식을 먼저 의심한다.
- 반환한 에셋의 메모리가 즉시 0이 되지 않을 수 있다(엔진 내부 해제 시점). 판단은 핸들·번들 수와 반복 후 누적 여부로 하고, 순간 수치로 하지 않는다.

### 7.4 계측과 산출물

| 도구 | 보는 것 | 산출물 |
|---|---|---|
| Addressables Profiler 모듈 | 로드된 번들·에셋 수와 참조 수 | 팝업 열기 → 캐릭터 전환 → 닫기 구간 캡처 또는 영상 |
| Memory Profiler 스냅샷 | 반복 전후 객체·메모리 비교 | 팝업 30회, 씬 왕복 30회 전후 비교표 |
| `ResourceCounters`(테스트 메뉴) | 이 프로젝트가 소유한 핸들·인스턴스 수 | Profiler 수치와 대조 |

- Addressables Profiler는 개발 빌드와 Addressables 진단 설정(프로파일러 이벤트, 빌드 레이아웃)이 필요하다. 설정 이름은 설치 버전 문서로 확인한다.
- 기록에는 기기, 빌드 설정(개발 빌드 여부), 그룹 구성, 캐시 상태, 반복 횟수를 함께 적는다.
- 해제 누락을 일부러 만든 빌드로 같은 구간을 찍어 비교하면 정상 동작의 근거가 된다(선택).

---

## 8. Jenkins 파이프라인

### 8.1 구성

| 노드 | 역할 | 요구 사항 |
|---|---|---|
| Windows (개발 PC) | 컨트롤러, 테스트, Addressables, Android 빌드, iOS Xcode 프로젝트 생성 | Unity 6000.3.9f1 + Android 모듈 + iOS 모듈, 라이선스 활성화된 사용자 계정으로 에이전트 실행 |
| macOS | iOS 빌드(`xcodebuild archive`, IPA 내보내기) | Xcode, 서명 인증서와 프로비저닝 프로파일 |

- 파이프라인은 저장소의 `Jenkinsfile`로 관리한다.
- Unity 실행은 배치 모드(`-batchmode -nographics -quit`)와 `-executeMethod`로 한다. 빌드 진입점은 `Assets/Editor/CI/` 아래 에디터 코드에 둔다.
- Windows 서비스로 Jenkins를 돌리면 로컬 시스템 계정으로 실행되어 Unity 라이선스를 찾지 못한다. 에이전트는 라이선스를 활성화한 사용자 계정으로 실행한다.

### 8.2 단계

| 단계 | 내용 | 실패 조건 |
|---|---|---|
| Checkout | 소스 받기, Library 캐시 유지 | — |
| EditMode 테스트 | `-runTests -testPlatform EditMode` | 실패 테스트 1건 이상 |
| PlayMode 테스트 | `-runTests -testPlatform PlayMode`, 지연·실패 주입 시나리오 포함 | 실패 테스트 1건 이상 |
| 리소스 무결성 검사 | [8.3](#83-리소스-무결성-검사) 항목 | 오류 1건 이상 |
| Addressables 빌드 | 매개변수로 전체 빌드 / 콘텐츠 업데이트 / 생략 선택 | 빌드 오류 |
| CCD 업로드 | 플랫폼 버킷에 동기화, 릴리스 생성, 배지 이동(UGS CLI 또는 CCD 관리 API) | 업로드·릴리스 오류 |
| 원격 검증 | 배지 주소로 카탈로그를 받아 참조 번들 존재와 크기 대조 | 누락 또는 불일치 |
| Android 빌드 | APK(개발 빌드), 필요 시 AAB | 빌드 오류 |
| iOS 빌드 | Windows에서 Xcode 프로젝트 생성 → macOS에서 archive | 빌드 오류 |
| 보관 | 빌드 산출물, 테스트 결과 XML, 빌드 로그, 빌드 리포트, content_state | — |

매개변수: `PLATFORM`(Android / iOS / All), `ADDRESSABLES_MODE`(Full / Update / Skip), `PROFILE`(Local / CCD_Dev), `CCD_BADGE`, `DEVELOPMENT_BUILD`.

- 콘텐츠 업데이트 모드는 직전 전체 빌드의 `addressables_content_state.bin`이 커밋돼 있어야 한다. 없으면 단계를 실패시킨다.

### 8.3 리소스 무결성 검사

- Addressables Analyze 규칙: 번들 간 중복 의존성, Resources 폴더와의 중복, 씬 의존성 중복.
- 주소 중복, 라벨 규칙 위반, 누락 참조(Missing) 검사.
- 그룹 위치 검사: Local 그룹이 로컬 경로, Remote 그룹이 CCD 경로를 쓰는지. Local 그룹이 업데이트 금지로 설정됐는지.
- Boot·패치 UI가 Addressables 에셋을 참조하지 않는지.
- 업로드 후 원격 카탈로그 기준으로 번들 존재와 크기를 대조.

### 8.4 기능 테스트 추가 규칙

- 작업 중 추가하는 기능 테스트는 EditMode 또는 PlayMode 테스트 어셈블리에 넣으면 파이프라인에 자동으로 포함된다.
- 실기기에서만 확인할 수 있는 항목(실제 네트워크 끊김, 메모리 관측)은 자동화하지 않고 수동 검증 기록으로 남긴다.

---

## 9. 작업 순서

기간은 정하지 않는다. 앞 단계의 종료 기준을 확인한 뒤 다음 단계로 넘어간다.

| 단계 | 작업과 종료 기준 |
|---|---|
| 1 (완료) | 로비 플로우, 던전 왕복, 임시 로더, 요청 번호·취소·소유권 규칙, 지연·실패 주입 메뉴 |
| 2 | Addressables 설치, 콘텐츠를 `Assets/LobbyFlowContent`로 옮기고 로컬·원격 그룹 구성, 데이터를 CSV로 전환, 로더 교체와 `Release` 경로 적용, 실제 패치 서비스. LocalServer 프로필로 v1 빌드 → `content/v2` 패치 1회 검증. Addressables Profiler로 팝업 열고 닫기 구간 캡처. Android 첫 플레이어 빌드(완료, 2026-11-05 CCD로 화면 확인) |
| 3 | CCD 연결(프로필 경로와 업로드만 교체), 챕터 진입 다운로드, CCD로 v2 패치 재검증(Windows 완료, 2026-11-05), 실기기 네트워크 끊김 검증 |
| 4 | Jenkins: 테스트, 무결성 검사, Addressables 빌드, CCD 업로드, 원격 검증, Android 빌드 |
| 5 | iOS 빌드 연결, 30회 반복 계측과 Memory Profiler 비교, 시연 영상, README |

---

## 10. 미결 사항

- [x] CCD 요금 — 대역폭 월 50GB 무료, 초과 GB당 $0.08(5.5). 결제 카드 등록 필요 여부는 대시보드에서 확인한다.
- [x] Unity Cloud 프로젝트 연결 — 조직 `frenil`, 프로젝트 `unity-lobby-flow`(ID `9211a594-db04-43bd-bd55-c6d120db13de`). CCD 환경은 production(5.4).
- [x] 첫 실행이 아닌 오프라인 진입 — 허용한다([6.4](#64-실패와-예외-처리)).
- [x] 카탈로그 형식 — JSON(6.2).
- [x] 로컬 그룹에 넣을 UI 이미지·사운드의 출처 — 빌더가 코드로 직접 그리고 합성한다.
- [x] iOS 빌드용 macOS 장비 — 보유 Mac을 Jenkins 에이전트로 연결한다.
- [ ] Mac의 macOS·Xcode 버전이 Unity 6000.3 iOS 빌드 요구 사항을 만족하는지.
- [ ] Mac에서 iOS 빌드 방식: Windows가 만든 Xcode 프로젝트를 넘겨받을지, Mac에 Unity를 설치해 직접 빌드할지.
- [ ] Apple Developer 계정 여부. 무료 계정은 실기기 설치가 7일 서명으로 제한되고 IPA 배포용 내보내기가 안 된다.
- [ ] iOS 실기기(iPhone/iPad) 보유 여부.
- [ ] Jenkins 설치 위치(개발 PC 상시 실행 여부).

---

## 11. 완료 기준과 산출물

- Android 빌드와 iOS 빌드 각 1개, 재현 방법. iOS 서명·배포 범위는 Apple Developer 계정 여부에 맞춰 명시한다.
- 정상 흐름, 패치 다운로드, 대표 예외 상황을 담은 60~90초 시연 영상.
- 팝업 열기와 닫기, 씬 왕복을 각각 30회 이상 반복한 확인 기록.
- Addressables Profiler에서 로드 후 증가하고 반환 후 감소하는 구간의 캡처 또는 영상.
- 반복 전후 Memory Profiler 스냅샷 비교 결과.
- 콘텐츠 업데이트 1회의 전후 기록(바뀐 번들, 받은 크기, 표시 결과). LocalServer 기록: `docs/records/2026-11-04-content-update-v2.md`. CCD로 다시 기록한다.
- 기기, 빌드 설정, 그룹 구성, 캐시 정책, 네트워크 조건, 반복 조건을 적은 로딩 지연과 메모리 관측 결과.
- 같은 조건의 수정 전후 결과(실제로 발견한 문제가 있을 때).
- Jenkins 파이프라인 실행 기록 1건 이상과 단계별 결과.
- 핵심 코드, 설계 판단, 남은 한계를 설명하는 짧은 README.
