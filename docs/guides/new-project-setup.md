# 새 프로젝트에 Addressables 배포 붙이기

`com.frenil.addressables-delivery` 패키지로 다른 Unity 프로젝트에 원격 콘텐츠 패치(빌드 → 업로드 → 앱에서 받기)와 Jenkins 자동화를 붙이는 순서다. 패키지 구성과 API는 [패키지 README](../../Packages/com.frenil.addressables-delivery/README.md)에 있다.

손으로 하는 일은 패키지 추가, 메뉴 두 번, 설정 에셋에 저장소 정보 입력, 그룹 구성, 시작 코드, Jenkins 작업 등록이다. 나머지 설정은 메뉴가 맞춘다.

## 0. 전제

| 항목 | 확인한 값 |
|---|---|
| Unity | 6000.3.9f1 (6000.3 이상) |
| Addressables | 2.8.1 |
| 플랫폼 | Windows, Android (iOS 플레이어 빌드는 아직 없음) |
| 원격 저장소 | Unity CCD. 다른 저장소(Custom)는 구현만 하고 실제 계정으로는 확인하지 않았다 |
| CI | Windows PC에 설치한 Jenkins |

## 1. 패키지 추가

`Packages/manifest.json`에 넣는다. Addressables와 uGUI는 의존성으로 함께 들어온다.

```json
{
  "dependencies": {
    "com.frenil.addressables-delivery": "https://github.com/Frenil-client/unity-lobby-flow.git?path=/Packages/com.frenil.addressables-delivery"
  },
  "testables": [
    "com.frenil.addressables-delivery"
  ]
}
```

`.gitignore`에 빌드 산출물과 빌드 도중에만 생기는 파일을 넣는다.

```gitignore
/ServerData/
/Builds/
/ci-logs/
/Assets/AddressableAssetsData/link.xml
/Assets/AddressableAssetsData/link.xml.meta
```

## 2. 프로젝트에 적용

1. **Tools ▸ Addressables Delivery ▸ 프로젝트에 적용**을 누른다.
   - `Assets/Resources/DeliverySettings.asset`이 생긴다. 공통 번들 접두어는 Product Name으로 채워진다.
   - Addressables 설정이 없으면 만들고, LocalServer와 CCD 프로필, 원격 카탈로그, JSON 카탈로그, 공통 번들 이름, 플레이어 빌드 옵션을 맞춘다.
2. 콘솔에 `ENABLE_JSON_CATALOG 심볼을 추가했다`가 나오면 컴파일이 끝날 때까지 기다린다. 빌드 창은 컴파일 전에는 빌드를 거부한다.
3. 콘솔 경고(비어 있는 CCD ID 등)는 3절을 마친 뒤 메뉴를 한 번 더 누르면 사라진다.
4. 이 상태로 EditMode 테스트를 돌리면 CCD 정보 검사(`CcdLocationIsComplete`)와 원격 참조 검사(`BuildScenesAndResourcesDoNotReferenceRemoteContent`)는 건너뜀(Ignored)으로 나온다. 3절과 4절을 마치면 검사한다.

## 3. 원격 저장소 준비

### CCD

1. [Unity Cloud](https://cloud.unity.com)에서 프로젝트를 만들고(또는 Unity 에디터 Services 창에서 연결) **Content Delivery**를 켠다. 무료 사용량을 넘으면 과금되므로 결제 수단 연결을 요구한다.
2. 환경(기본 `production`)에 플랫폼마다 버킷을 하나씩 만든다. **공개(Public)**로 만든다. 공개 여부는 나중에 바꿀 수 없고, Addressables는 인증 없이 받으므로 공개여야 한다.
3. 업로드용 **서비스 계정**을 만들고 CCD 관리 권한을 준다. Key ID와 Secret Key는 저장소에 넣지 않는다.
4. 개발 PC에 [UGS CLI](https://services.docs.unity.com/guides/ugs-cli/latest/general/overview)를 설치한다. 키는 사용자 환경 변수 `UGS_CLI_SERVICE_KEY_ID`, `UGS_CLI_SERVICE_SECRET_KEY`로 두고, Unity를 다시 연다.
5. **Tools ▸ Addressables Delivery ▸ 설정 에셋 선택**으로 설정 에셋을 열어 채운다.

| 항목 | 값 |
|---|---|
| Storage | `Ccd` |
| Ccd Project Id | Unity Cloud 프로젝트 ID |
| Ccd Environment | `production` |
| Windows Bucket / Android Bucket | 버킷 이름과 ID. 쓰지 않는 플랫폼은 비운다 |
| Release Badge | `latest` |
| Test Badges | 개발 빌드에서 고를 배지. 처음에는 `latest` 하나 |

### 다른 저장소 (Custom)

1. 저장소 공개 주소와 그 저장소에 올리는 명령행 도구(예: AWS CLI, rclone)를 준비한다.
2. 설정 에셋에서 `Storage`를 `Custom`으로 두고 채운다.

| 항목 | 예 |
|---|---|
| Custom Load Url | `https://cdn.example.com/game/[BuildTarget]` |
| Custom Upload Command | `aws s3 sync "{folder}" s3://my-bucket/game/{platform}/` |

3. **프로젝트에 적용**을 다시 누르면 원격 프로필 이름이 `Remote`로 만들어진다.

## 4. 그룹 구성

그룹과 라벨은 프로젝트가 정한다. 패키지 테스트가 아래 규칙을 검사한다(Test Runner ▸ EditMode).

- 앱에 들어가 바뀌지 않는 것은 로컬 그룹(Local Build/Load Path, 정적 콘텐츠), 패치로 바꾸는 것은 원격 그룹(Remote Build/Load Path, 정적 아님)에 둔다.
- 시작할 때 받을 원격 콘텐츠에 라벨(예: `patch_boot`)을, 들어갈 때 받을 콘텐츠에 다른 라벨(예: `chapter02`)을 붙인다.
- 씬은 파일 항목으로 등록한다. 씬과 다른 에셋을 한 폴더 항목에 넣지 않는다.
- 여러 번들이 함께 쓰는 에셋(URP Lit 같은 셰이더, TMP 폰트, 입력 액션, 공통 효과음)은 로컬 공유 그룹에 따로 넣는다. 넣지 않으면 쓰는 번들마다 복사된다. `BundleLayoutTests`가 어느 에셋이 어느 번들에 복사됐는지 알려 준다.
- 빌드 씬과 `Resources`에서 원격 그룹 에셋을 직접 참조하지 않는다. 직접 참조하면 플레이어에 들어가 패치로 바꿀 수 없다.
- 동적 TMP 폰트를 Addressables로 쓰면 그 폴더를 설정 에셋 `Dynamic Font Folders`에 넣는다. TextMesh Pro의 `TMP_SDF-Mobile.shader`에 있는 `#pragma enable_d3d11_debug_symbols`는 지운다(Windows 번들 해시가 PC마다 달라진다).

## 5. 시작 코드

시작 씬에서 패치 서비스를 부른다. 시작할 때 자동 갱신은 꺼져 있으므로 `CheckAsync`를 불러야 원격 변경이 반영된다.

```csharp
using AddressablesDelivery.Patching;

var patch = new AddressablesPatchService("patch_boot");
try
{
    var check = await patch.CheckAsync(token);
    if (check.HasUpdate)
        await patch.DownloadAsync("patch_boot", p => progressBar.value = p.Ratio, token);
}
catch (PatchNetworkException)
{
    if (!await patch.HasCachedContentAsync(token))
    {
        // 처음 실행인데 서버에 닿지 못했다. 재시도를 안내한다.
    }
    // 받아 둔 콘텐츠가 있으면 그대로 시작한다.
}
```

화면 흐름만 먼저 만들 때는 `SimulatedPatchService`로 바꾸고 `PatchSimulationSettings.Mode`로 최신, 업데이트 있음, 오프라인을 고른다.

## 6. 첫 빌드와 확인

**Tools ▸ Addressables Delivery ▸ 빌드 창**에서 한다.

1. 로컬에서 먼저 본다.
   1. 프로필 `LocalServer` → **전체 빌드** → **콘텐츠 서버 시작**.
   2. **Windows 개발 빌드**를 실행한다. 콘텐츠 서버 로그에 받은 카탈로그와 번들이 나온다.
   3. Android 기기는 USB로 연결하고 **Android 개발 빌드 후 기기 실행**(기기의 localhost를 PC 서버로 잇는다).
2. 원격 저장소로 바꾼다.
   1. 프로필 `CCD`(또는 `Remote`) → **전체 빌드** → **현재 플랫폼 업로드**.
   2. **연결 확인**에 카탈로그 해시가 나오면 공개 주소에서 받을 수 있다.
   3. 개발 빌드를 만든다. 이 빌드가 출시본(첫 버전) 역할이다.
3. 전체 빌드가 만든 `Assets/AddressableAssetsData/<플랫폼>/addressables_content_state.bin`을 커밋한다. 이후 콘텐츠 업데이트의 기준이다.

플랫폼을 바꾸거나 프로필을 바꾸면 그 플랫폼에서 전체 빌드를 다시 한다. 빌드 창은 Addressables 빌드가 현재 프로필과 맞지 않으면 플레이어 빌드와 업로드를 막는다.

## 7. 콘텐츠 업데이트

1. 원격 그룹의 콘텐츠를 바꾼다(로컬 그룹은 바꾸지 않는다).
2. **콘텐츠 업데이트 빌드** → **현재 플랫폼 업로드**.
3. 6절에서 만든 플레이어를 켜면 카탈로그가 갱신되고 바뀐 번들만 받는다.

업데이트 빌드 뒤에 만든 플레이어는 이미 새 카탈로그를 갖고 있어 출시본을 대신하지 못한다. 로컬 그룹을 바꿔야 하면 앱을 새로 출시하는 것이라 전체 빌드로 기준을 다시 만든다.

## 8. Jenkins

1. **Tools ▸ Addressables Delivery ▸ CI 파일 내보내기**를 누른다. 프로젝트 루트에 `Jenkinsfile`, `ci/unity.ps1`, `ci/integrity.ps1`이 생긴다. Unity 버전, 플랫폼, 업로드 인증 블록, 기준 파일을 검사할 플랫폼이 채워져 있다. 이미 있으면 `.new`로 쓰니 비교해 합친다.
2. 셋을 커밋하고 푸시한다. `ci/*.ps1`은 UTF-8 BOM으로 써진다(Windows PowerShell 5.1이 BOM 없는 한글 스크립트를 깨뜨린다).
3. Jenkins 설치, 에이전트 레이블(`unity-windows`), 환경 변수(`UNITY_EXE`, `UGS_EXE`), Credentials 등록은 [Jenkins 설치와 등록](jenkins-setup.md) 1~3절과 같다. Custom 저장소는 `Jenkinsfile`의 `withUploadCredentials`에 쓰는 키를 넣는다.
4. Pipeline 작업을 만들고 Script Path를 `Jenkinsfile`로 둔다.

| 하고 싶은 일 | ADDRESSABLES | BUILD_PLAYER | UPLOAD | BADGE |
|---|---|---|---|---|
| 검사와 테스트만 | none | 끔 | 끔 | |
| 출시본과 첫 콘텐츠 | full | 켬 | 켬 | |
| 콘텐츠 업데이트 배포 | update | 끔 | 켬 | (선택) |

- `main` 폴링으로 시작된 실행은 무결성 검사와 테스트만 한다. 빌드와 업로드는 파라미터를 골라 누른 실행에서만 하고, 실행 중에 다시 묻지 않는다.
- `full`을 돌렸으면 산출물 `ci-logs/baseline/<플랫폼>/addressables_content_state.bin`을 저장소에 커밋한다. 실행마다 새로 체크아웃하므로 커밋하지 않은 기준은 다음 실행에서 사라진다.
- 업로드 단계는 공개 주소의 카탈로그 해시와 올린 파일 크기를 확인한다. CCD는 배지를 옮긴 뒤 반영까지 1~2분 걸려 최대 4분 다시 받는다.

## 9. 문제가 생기면

| 증상 | 원인과 조치 |
|---|---|
| 플레이어가 원격 주소를 만들지 못한다(로그 `Resources/DeliverySettings 가 없다`) | 설정 에셋이 `Resources` 폴더가 아니거나 이름이 다르다. `DeliverySetupTests.SettingsAssetIsInResources` |
| 빌드 결과 카탈로그가 `.bin` | JSON 카탈로그 심볼이 컴파일되기 전에 빌드했다. 컴파일 뒤 다시 빌드한다 |
| CI와 로컬에서 같은 커밋인데 번들 해시가 다르다 | 공통 번들 접두어, 동적 폰트, D3D11 디버그 셰이더를 확인한다(`ci/integrity.ps1`, 패키지 테스트) |
| 업데이트 뒤 정적 그룹 에셋이 씬 번들을 가리킨다 | 폴더 항목에 씬과 다른 에셋이 섞였다. 씬을 파일 항목으로 나누고 기준을 다시 만든다 |
| 원격 검증이 4분 뒤에도 실패 | 버킷이 비공개이거나 배지가 다른 릴리스를 가리킨다. 빌드 창 **연결 확인**으로 본다 |
