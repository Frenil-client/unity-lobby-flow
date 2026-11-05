# Jenkins 설치와 등록 (Windows 개발 PC)

`Jenkinsfile`을 돌리기 위한 Jenkins 설정 순서다. 구성과 단계는 SPEC 8장을 따른다. Jenkins 한 대가 컨트롤러이자 Unity 빌드 에이전트(레이블 `unity-windows`)다.

## 1. 설치

1. Java 21(또는 17)을 설치한다. 예: Eclipse Temurin JDK 21 Windows x64 MSI. 설치 중 `JAVA_HOME` 설정을 켠다.
2. [jenkins.io](https://www.jenkins.io/download/)에서 **Windows용 LTS** 설치 파일(.msi)을 받아 실행한다.
3. **Service Logon Credentials**에서 **Run service as local or domain user**를 고르고, Unity 라이선스를 활성화한 Windows 계정(예: `.\user`)과 비밀번호를 넣는다. **Test Credentials**가 통과해야 다음으로 넘어간다.
   - 로컬 시스템 계정으로 두면 Unity가 라이선스를 찾지 못한다.
4. **Port**는 `8090`으로 바꾼다. `8080`은 프로젝트의 로컬 콘텐츠 서버가 쓴다.
5. 설치가 끝나면 `http://localhost:8090`을 연다. 첫 화면이 안내하는 파일에서 초기 관리자 비밀번호를 읽어 넣는다.
6. **Install suggested plugins**를 고른다. 이어서 **Jenkins 관리 ▸ Plugins ▸ Available plugins**에서 아래를 추가로 설치한다.
   - `PowerShell`: `powershell` 단계
   - `NUnit`: Unity 테스트 결과(NUnit XML) 게시
   - 추천 묶음에 이미 있는 것: Pipeline, Git, Credentials Binding, Timestamper

## 2. 에이전트(내장 노드) 설정

**Jenkins 관리 ▸ Nodes ▸ Built-In Node ▸ Configure**

| 항목 | 값 |
|---|---|
| Number of executors | `1` (Unity 빌드는 한 번에 하나) |
| Labels | `unity-windows` |
| Node Properties ▸ Environment variables | `UNITY_EXE` = `C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe` |
| | `UGS_EXE` = `C:\Users\<계정>\.local\bin\ugs.exe` (UGS CLI 위치) |

Git은 PATH에 있는 Git for Windows를 쓴다(`C:\Program Files\Git\cmd\git.exe`).

## 3. CCD 키 등록 (작성자만)

**Jenkins 관리 ▸ Credentials ▸ System ▸ Global credentials ▸ Add Credentials**, Kind는 **Secret text**로 두 개를 만든다.

| ID | Secret |
|---|---|
| `ugs-service-key-id` | 서비스 계정 Key ID |
| `ugs-service-secret-key` | 서비스 계정 Secret Key |

키는 여기에만 둔다. 저장소와 빌드 로그에는 남지 않는다(Jenkins가 로그에서 가린다). 업로드를 하지 않으면 이 단계는 건너뛰어도 된다.

## 4. 파이프라인 작업 만들기

1. **새로운 Item** → 이름 `lobby-flow` → **Pipeline**.
2. **Pipeline ▸ Definition**: `Pipeline script from SCM`
   - SCM: `Git`, Repository URL: `https://github.com/Frenil-client/unity-lobby-flow.git`
   - Branch Specifier: `*/main`, Script Path: `Jenkinsfile`
3. 저장한다.

## 5. 첫 실행

1. **지금 빌드**를 누른다. 첫 실행은 Jenkinsfile의 파라미터와 폴링 설정을 등록하면서 기본값으로 돈다(Android, `update`, 플레이어 빌드, 업로드 없음).
   - 작업 폴더에서 Unity가 처음 프로젝트를 열기 때문에 `Library/` 임포트로 오래 걸린다. 다음 실행부터는 작업 폴더를 그대로 쓴다.
2. 두 번째부터는 **파라미터와 함께 빌드**에서 고른다.

| 하고 싶은 일 | PLATFORM | CONTENT | ADDRESSABLES | BUILD_PLAYER | UPLOAD_CCD | CCD_BADGE |
|---|---|---|---|---|---|---|
| 검사와 테스트만 | 아무거나 | main | none | 끔 | 끔 | none |
| v1 APK 만들기 | Android | main | none | 켬 | 끔 | none |
| v2 패치를 CCD에 배포 | Android | content/v2 | update | 끔 | 켬 | v2 |

`main`에 커밋이 올라오면 15분 안에 폴링으로 무결성 검사와 EditMode 테스트가 자동으로 돈다.

## 6. 확인할 곳

- 단계별 Unity 로그: 빌드 결과의 **Build Artifacts ▸ ci-logs/**
- 테스트 결과: 빌드 결과의 **Test Result**
- APK, Windows zip: **Build Artifacts**
- CCD 업로드 단계는 승인 버튼(**업로드**)을 누를 때까지 기다린다. 업로드 뒤 공개 주소의 카탈로그 해시가 올린 것과 같은지 자동으로 확인한다.

## 문제 해결

| 증상 | 조치 |
|---|---|
| 설치 중 Test Credentials가 `0x8007052e`(로그온 실패) | 계정 이름이나 비밀번호가 틀렸거나, 계정에 비밀번호가 없다. Windows는 비밀번호 없는 계정으로 서비스를 실행하지 못한다. 설정 ▸ 계정 ▸ 로그인 옵션 ▸ 비밀번호에서 비밀번호를 만든 뒤 다시 확인한다(PIN이 아니라 비밀번호를 넣는다) |
| 설치 중 "does not have the privilege to logon as a service" | 계정에 "서비스로 로그온" 권한이 없다. Windows Home에는 이 권한을 주는 로컬 보안 정책 편집기가 없다. 설치는 LocalSystem으로 끝내고, 서비스(services.msc) ▸ Jenkins ▸ 속성 ▸ 로그온에서 계정을 바꾸면 Windows가 권한을 함께 준다. 그다음 서비스를 다시 시작한다 |
| 설치 중 "Failed to find compatible Java version" | Java 경로가 JDK 폴더가 아니다. **Change**로 JDK 설치 폴더(예: `C:Program FilesJavajdk-21…`, `bin` 위 폴더)를 고른다 |
| 첫 실행이 "Unable to find Jenkinsfile" | Jenkinsfile 이 들어간 커밋이 아직 GitHub `main`에 없다. 푸시한 뒤 다시 실행한다 |
| Unity가 라이선스 오류로 끝남 | 서비스 계정이 라이선스를 활성화한 사용자인지 확인한다(서비스 ▸ Jenkins ▸ 속성 ▸ 로그온) |
| `powershell` 단계에서 한글이 깨지거나 구문 오류 | `ci/*.ps1`이 UTF-8 BOM으로 저장돼 있는지 확인한다 |
| `ugs 를 찾지 못했다` | 노드 환경 변수 `UGS_EXE` 경로를 확인한다 |
| 포트 충돌 | Jenkins 포트를 8080 이외로 둔다(설치 폴더의 `jenkins.xml`의 `--httpPort`) |
