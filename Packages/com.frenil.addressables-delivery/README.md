# Addressables Delivery

Addressables 원격 콘텐츠를 만들고, 올리고, 앱에서 받는 과정을 묶은 Unity 패키지다. [unity-lobby-flow](https://github.com/Frenil-client/unity-lobby-flow) 데모에서 Windows와 Android 실기기, Unity CCD, Jenkins로 확인한 코드를 프로젝트 고유값과 분리해 옮겼다.

| 기능 | 내용 |
|---|---|
| 프로젝트에 적용 | LocalServer/원격 프로필, 원격 카탈로그, JSON 카탈로그 심볼, 공통 번들 이름 고정, 플레이어 빌드와 분리, http 개발 빌드 한정을 한 번에 맞춘다 |
| 빌드 창 | 프로필 전환, 전체 빌드와 콘텐츠 업데이트 빌드, 개발 플레이어 빌드, 로컬 콘텐츠 서버, 업로드. 순서를 어기는 버튼은 막는다 |
| 빌드 검사 | 컴파일 중, JSON 심볼 불일치, 비어 있는 CCD ID, 현재 프로필과 다른 Addressables 빌드로 만드는 플레이어 빌드를 거부한다 |
| 업로드 | CCD(UGS CLI, 플랫폼별 버킷, 배지) 또는 직접 적은 명령(S3, R2, 자체 서버 등). 업로드 뒤 공개 주소의 카탈로그 해시와 파일 크기를 확인한다 |
| 런타임 | 시작 패치 서비스(카탈로그 확인과 갱신, 라벨 다운로드, 진행률, 오프라인 판정), 모의 패치, 화면 단위 핸들 묶음, 보유 리소스 수 |
| 테스트 | 설정 검사, 그룹 규칙, Analyze 번들 중복, 빌드 씬과 Resources의 원격 에셋 직접 참조 |
| CI | Jenkins 배치 진입점과 템플릿(Jenkinsfile, Unity 실행, 저장소 무결성 검사) 내보내기 |

새 프로젝트에 붙이는 순서는 [새 프로젝트 적용 가이드](https://github.com/Frenil-client/unity-lobby-flow/blob/main/docs/guides/new-project-setup.md)에 있다. 이 문서는 구성과 API를 정리한다.

## 요구 사항

- Unity 6000.3 이상, Addressables 2.8.1, uGUI 2.0(TextMeshPro). 확인한 조합은 Unity 6000.3.9f1 + Addressables 2.8.1이다.
- CCD 업로드는 [UGS CLI](https://services.docs.unity.com/guides/ugs-cli/latest/general/overview)와 서비스 계정 키.
- CI 템플릿은 Windows Jenkins 에이전트(PowerShell)를 기준으로 한다.

## 설치

`Packages/manifest.json`:

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

`testables`에 넣어야 패키지의 EditMode 테스트가 Test Runner와 CI에 함께 나온다. 버전을 고정하려면 주소 끝에 `#커밋` 또는 `#태그`를 붙인다.

## 메뉴

| 메뉴 | 하는 일 |
|---|---|
| Tools ▸ Addressables Delivery ▸ 빌드 창 | 빌드, 프로필, 콘텐츠 서버, 업로드 |
| Tools ▸ Addressables Delivery ▸ 프로젝트에 적용 | 설정 에셋 만들기(없을 때)와 Addressables, PlayerSettings 설정. 여러 번 실행해도 결과가 같다. 그룹은 건드리지 않는다 |
| Tools ▸ Addressables Delivery ▸ 설정 에셋 선택 | `DeliverySettings` 를 인스펙터에 연다 |
| Tools ▸ Addressables Delivery ▸ CI 파일 내보내기 | 프로젝트 루트에 `Jenkinsfile`, `ci/unity.ps1`, `ci/integrity.ps1` 을 쓴다. 이미 있으면 `.new` 로 쓴다 |

## 설정 에셋 (`DeliverySettings`)

프로젝트마다 다른 값은 모두 여기 둔다. 플레이어가 실행 중에 CCD 주소를 읽기 때문에 `Resources/DeliverySettings.asset` 이어야 한다(프로젝트에 적용이 `Assets/Resources` 에 만든다).

| 항목 | 내용 |
|---|---|
| Storage | `Ccd` 또는 `Custom` |
| Ccd Project Id, Ccd Environment | Unity Cloud 프로젝트 ID와 환경 이름(기본 `production`) |
| Windows / Android / iOS Bucket | 버킷 이름(업로드)과 ID(다운로드 주소). 쓰지 않는 플랫폼은 비운다 |
| Release Badge | 출시 빌드가 받는 배지. 기본 `latest`(CCD가 새 릴리스마다 옮긴다) |
| Test Badges | 개발 빌드에서 고를 수 있는 배지. 첫 값이 기본이다 |
| Ccd Url Class | CCD 프로필 주소가 값을 읽는 클래스. 기본 `AddressablesDelivery.CcdLocation` |
| Custom Load Url | Custom 저장소의 받는 주소. 예: `https://cdn.example.com/[BuildTarget]` |
| Custom Upload Command | Custom 저장소에 올리는 명령. `{folder}` `{platform}` `{badge}` `{notes}` 를 바꿔 넣는다 |
| Shared Bundle Prefix | 공통 번들(내장 셰이더, 스크립트) 이름 접두어 |
| Player Name | 플레이어 빌드 파일 이름 |
| Dynamic Font Folders | 빌드 전과 저장 직전에 동적 데이터를 비울 TMP 폰트 폴더 |
| Local Server Port | LocalServer 프로필과 콘텐츠 서버 포트(기본 8080) |

프로젝트 ID와 버킷 ID는 공개 다운로드 주소의 일부라 비밀값이 아니다. 업로드 키는 설정 에셋에 넣지 않는다.

## 런타임 사용

```csharp
using AddressablesDelivery.Patching;

const string BootLabel = "patch_boot"; // 시작할 때 받아야 하는 원격 콘텐츠의 라벨
IPatchService patch = new AddressablesPatchService(BootLabel);

try
{
    var check = await patch.CheckAsync(token);          // 원격 카탈로그 확인과 갱신, 받을 크기 계산
    if (check.HasUpdate)
        await patch.DownloadAsync(BootLabel, p => bar.value = p.Ratio, token);
}
catch (PatchNetworkException)
{
    // 서버에 닿지 못했다. 이미 받아 둔 콘텐츠가 있으면 오프라인으로 시작하고, 없으면 재시도를 안내한다.
    var canStartOffline = await patch.HasCachedContentAsync(token);
}

// 챕터처럼 들어갈 때 받는 콘텐츠
var bytes = await patch.GetDownloadSizeAsync("chapter02", token);
```

- 시작할 때 Addressables가 카탈로그를 자동으로 갱신하지 않게 설정되므로(프로젝트에 적용), `CheckAsync` 를 한 번 부르기 전에는 원격 변경이 반영되지 않는다.
- 모든 Addressables 핸들은 서비스 안에서 반환한다. 취소 요청을 받아도 진행 중인 작업이 끝나기를 기다린 뒤 반환하고 취소 예외를 던진다.
- `SimulatedPatchService` 와 `PatchSimulationSettings` 는 네트워크 없이 최신, 업데이트 있음, 오프라인(캐시 있음/없음), 다운로드 끊김을 흉내 낸다. 화면 흐름 검증용이며 실제 네트워크 장애 검증이 아니다.
- `AssetHandleSet` 은 화면 하나가 로드한 리소스를 묶어 `Dispose` 한 번으로 반환한다. 반환 뒤 도착한 결과는 null 로 돌려준다. `ResourceCounters` 로 반복 뒤 보유 수가 시작 값으로 돌아오는지 본다.
- `DeliveryLog.Handler` 로 패키지 로그를 프로젝트 로그 형식에 맞출 수 있다.
- 개발 빌드는 `CcdLocation.TestBadge` 로 받을 배지를 고를 수 있다. 고른 배지는 다음 `CheckAsync` 에서 적용되므로 앱을 다시 시작하지 않아도 된다(요청 주소의 배지 부분을 `Addressables.InternalIdTransformFunc` 로 바꾼다. 프로젝트가 이미 넣은 변환이 있으면 그 결과에 이어서 적용한다). `CheckAsync` 는 원격 번들을 쓰는 화면이 없을 때 부른다.

## CI 진입점

`AddressablesDelivery.Editor.CiBuild` 의 정적 메서드를 `-executeMethod` 로 부른다. 메서드가 종료 코드를 돌려주므로 `-quit` 는 붙이지 않는다.

| 메서드 | 인자 | 하는 일 |
|---|---|---|
| `Addressables` | `-deliveryProfile 이름`(기본 원격 프로필), `-deliveryMode full\|update` | 전체 빌드 또는 콘텐츠 업데이트 빌드 |
| `Player` | 없음 | 현재 플랫폼 개발 빌드(Windows, Android) |
| `Upload` | `-deliveryBadge 이름\|none`, `-deliveryNotes 메모` | 업로드 후 공개 주소의 카탈로그 해시와 모든 파일 크기 확인 |

## 이 패키지가 막는 문제

데모를 만들면서 실제로 겪은 문제를 기본 동작과 테스트로 막는다.

| 문제 | 막는 곳 |
|---|---|
| 공통 번들 이름이 프로젝트 폴더 이름 해시라 CI 작업 폴더와 로컬에서 번들 해시가 다르다 | 프로젝트에 적용(접두어 고정), `DeliverySetupTests` |
| 설정만 켜고 `ENABLE_JSON_CATALOG` 심볼이 없어 바이너리 카탈로그가 나온다 | 프로젝트에 적용(심볼 추가), 빌드 거부, 테스트 |
| 다른 프로필이나 플랫폼으로 빌드한 Addressables 결과로 플레이어를 빌드해 이전 원격 주소가 들어간다 | 플레이어 빌드 거부, 빌드 창 버튼 막기 |
| 플레이어 빌드가 Addressables 전체 빌드를 몰래 해 업데이트 기준이 바뀐다 | 프로젝트에 적용(Do Not Build With Player) |
| 폴더 항목에 씬과 다른 에셋을 함께 두면 업데이트 빌드가 정적 그룹 에셋 위치를 씬 번들로 바꿔 쓴다(Addressables 2.8.1) | `DeliverySetupTests.FolderEntriesDoNotMixScenesWithOtherAssets` |
| 그룹에 넣지 않은 공유 에셋(셰이더, 폰트, 입력 액션)이 쓰는 번들마다 복사된다 | `BundleLayoutTests`(Analyze 중복 검사) |
| 에디터가 동적 TMP 폰트에 글자를 채운 채 저장해 폰트 번들 해시가 흔들린다 | 저장 직전과 빌드 전 비우기, `ci/integrity.ps1` |
| TMP 셰이더의 D3D11 디버그 정보 때문에 Windows 번들 해시가 PC마다 다르다 | `ci/integrity.ps1` |
| 빌드 도중 상태(`preloadedAssets`, `link.xml`)가 커밋된다 | 플레이어 빌드 뒤 되돌리기, `ci/integrity.ps1` |
| 업로드가 끝났다고 보고했지만 공개 주소에 반영되지 않았거나 파일이 빠졌다 | `CiBuild.Upload` 원격 검증 |

## 주의

- CCD 프로필 주소에 들어간 클래스 이름(`{클래스.BucketId}` 등)은 카탈로그의 번들 주소에 그대로 남는다. 배포한 뒤 이 클래스 이름을 바꾸면 카탈로그가 달라져 업데이트 기준을 다시 만들어야 한다. 다른 이름으로 이미 배포했다면 그 이름의 클래스를 남겨 `CcdLocation` 으로 넘겨주고 `Ccd Url Class` 에 그 이름을 둔다.
- 전체 빌드가 만든 `Assets/AddressableAssetsData/<플랫폼>/addressables_content_state.bin` 은 출시한 플레이어의 기준이라 커밋한다. CI 에서 전체 빌드를 했으면 산출물의 기준 파일을 커밋한다.
- 콘텐츠 업데이트 빌드도 로컬 카탈로그를 새로 쓴다. 출시본 역할을 하는 플레이어는 전체 빌드 직후, 업데이트 빌드 전에 만든다.
- Custom 저장소는 명령 실행과 공개 주소 검증까지만 구현했다. 실제 S3/R2 계정으로는 확인하지 않았다.
- iOS 플레이어 빌드와 macOS CI 에이전트는 아직 없다(설정과 버킷 항목은 있다).
