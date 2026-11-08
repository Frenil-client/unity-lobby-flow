// Lobby Flow 빌드 파이프라인(SPEC 8).
// 에이전트: Unity 6000.3.9f1 과 라이선스가 있는 Windows PC(레이블 unity-windows). 설치와 설정은 docs/guides/jenkins-setup.md.
// 배치 진입점은 패키지 com.frenil.addressables-delivery 의 CiBuild 다. 이 파일은 패키지 템플릿(Templates~/Jenkinsfile)에 v1/v2 콘텐츠 릴리스를 더한 것이다.
// main 폴링으로 시작된 실행은 무결성 검사와 EditMode, PlayMode 테스트만 한다. 빌드와 CCD 업로드는 사람이 파라미터를 골라 실행한다.
//   MODE=custom          : 플랫폼 하나에 대해 고른 단계(콘텐츠 출처, Addressables, 플레이어, 업로드)만 한다.
//   MODE=content-release : 고른 플랫폼마다 v1 전체 빌드 → 플레이어 → CCD v1 → v2 덮기 → 업데이트 빌드 → CCD v2 를 한 번에 하고,
//                          새 업데이트 기준 파일(addressables_content_state.bin)을 산출물로 남긴다. 커밋은 사람이 한다.
pipeline {
    agent { label 'unity-windows' }

    options {
        timestamps()
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '5'))
    }

    triggers {
        pollSCM('H/15 * * * *')
    }

    parameters {
        choice(name: 'MODE', choices: ['custom', 'content-release'], description: 'custom: 아래 단계를 골라 플랫폼 하나만. content-release: v1/v2 를 다시 빌드해 CCD 에 올리는 전체 절차(RELEASE_PLATFORMS 사용)')
        choice(name: 'RELEASE_PLATFORMS', choices: ['both', 'StandaloneWindows64', 'Android'], description: 'content-release 에서 처리할 플랫폼')
        choice(name: 'PLATFORM', choices: ['Android', 'StandaloneWindows64'], description: 'custom: Unity -buildTarget')
        choice(name: 'CONTENT', choices: ['main', 'content/v2'], description: 'custom: 원격 콘텐츠(Assets/LobbyFlowContent/Remote) 출처. content/v2 는 v2 패치를 만들 때 쓴다')
        choice(name: 'ADDRESSABLES', choices: ['update', 'full', 'none'], description: 'custom: update 는 커밋된 기준으로 콘텐츠 업데이트 빌드, full 은 기준을 새로 만든다')
        booleanParam(name: 'BUILD_PLAYER', defaultValue: true, description: 'custom: 개발 빌드(Windows 실행 파일 또는 APK)를 만들어 보관한다')
        booleanParam(name: 'UPLOAD_CCD', defaultValue: false, description: 'custom: CCD 에 올리고 릴리스를 만든다')
        choice(name: 'CCD_BADGE', choices: ['none', 'v1', 'v2'], description: 'custom: 새 릴리스에 붙일 콘텐츠 버전 배지(latest 는 CCD 가 옮긴다)')
    }

    environment {
        // 에이전트에 UNITY_EXE, UGS_EXE 환경 변수를 두면 그 경로를 쓴다.
        CI_LOGS = 'ci-logs'
    }

    stages {
        stage('무결성 검사') {
            steps {
                powershell 'ci/integrity.ps1'
            }
        }

        stage('EditMode 테스트') {
            steps {
                // -runTests 는 끝나면 스스로 종료한다(0 통과, 2 실패 있음).
                unity('editmode-tests', params.PLATFORM, "'-runTests','-testPlatform','EditMode','-testResults',\"\$PWD/ci-logs/editmode-results.xml\"")
            }
            post {
                always {
                    nunit testResultsPattern: 'ci-logs/editmode-results.xml', failIfNoResults: false
                }
            }
        }

        stage('PlayMode 테스트') {
            steps {
                // 미리보기 요청 규칙, 지연·실패 주입, 모의 패치를 실제 프레임 진행으로 검증한다(SPEC 8.4). 네트워크는 쓰지 않는다.
                unity('playmode-tests', params.PLATFORM, "'-runTests','-testPlatform','PlayMode','-testResults',\"\$PWD/ci-logs/playmode-results.xml\"")
            }
            post {
                always {
                    nunit testResultsPattern: 'ci-logs/playmode-results.xml', failIfNoResults: false
                }
            }
        }

        stage('원격 콘텐츠 선택') {
            when { expression { isCustomRun() && params.CONTENT == 'content/v2' } }
            steps {
                useRemoteContent('content/v2')
            }
        }

        stage('Addressables 빌드') {
            when { expression { isCustomRun() && params.ADDRESSABLES != 'none' } }
            steps {
                unity('addressables', params.PLATFORM, addressablesArgs(params.ADDRESSABLES))
            }
        }

        stage('플레이어 빌드') {
            when { expression { isCustomRun() && params.BUILD_PLAYER } }
            steps {
                unity('player', params.PLATFORM, playerArgs())
                archivePlayer(params.PLATFORM)
            }
        }

        stage('CCD 업로드') {
            when { expression { isCustomRun() && params.UPLOAD_CCD } }
            steps {
                withCcdKeys {
                    unity('ccd-upload', params.PLATFORM, uploadArgs(params.CCD_BADGE))
                }
            }
        }

        stage('콘텐츠 릴리스') {
            when { expression { isManualRun() && params.MODE == 'content-release' } }
            steps {
                script {
                    def targets = params.RELEASE_PLATFORMS == 'both' ? ['StandaloneWindows64', 'Android'] : [params.RELEASE_PLATFORMS]
                    for (target in targets) {
                        def folder = platformFolder(target)
                        def tag = folder.toLowerCase()

                        // v1: 기준을 새로 만들고, 그 기준으로 만든 플레이어가 출시본(v1 설치본) 역할을 한다.
                        stage("${folder} v1") {
                            useRemoteContent('main')
                            unity("${tag}-v1-addressables", target, addressablesArgs('full'))
                            unity("${tag}-player", target, playerArgs())
                            archivePlayer(target)
                            powershell """
                                New-Item -ItemType Directory -Force ci-logs/baseline/${folder} | Out-Null
                                Copy-Item Assets/AddressableAssetsData/${folder}/addressables_content_state.bin ci-logs/baseline/${folder}/
                            """
                            withCcdKeys {
                                unity("${tag}-v1-upload", target, uploadArgs('v1'))
                            }
                        }

                        // v2: 체크아웃 없이 같은 작업 폴더에서 방금 만든 기준으로 업데이트 빌드를 한다.
                        stage("${folder} v2") {
                            useRemoteContent('content/v2')
                            unity("${tag}-v2-addressables", target, addressablesArgs('update'))
                            withCcdKeys {
                                unity("${tag}-v2-upload", target, uploadArgs('v2'))
                            }
                            useRemoteContent('main')
                        }
                    }
                }
            }
        }
    }

    post {
        always {
            // content/v2 로 덮은 Remote/ 를 되돌려 다음 실행이 main 상태에서 시작하게 한다.
            useRemoteContent('main')
            archiveArtifacts artifacts: 'ci-logs/*.log, ci-logs/*.xml, ci-logs/baseline/**', allowEmptyArchive: true
        }
    }
}

// 사람이 눌러 시작한 실행인지. main 폴링으로 시작된 실행은 검사와 테스트만 한다.
def isManualRun() {
    return currentBuild.getBuildCauses('hudson.triggers.SCMTrigger$SCMTriggerCause').isEmpty()
}

def isCustomRun() {
    return isManualRun() && params.MODE == 'custom'
}

// Unity 를 배치 모드로 한 번 실행한다. 로그는 ci-logs/<name>.log.
def unity(String name, String target, String arguments) {
    powershell """
        ci/unity.ps1 -Name ${name} -BuildTarget ${target} -Arguments ${arguments}
        exit \$LASTEXITCODE
    """
}

def addressablesArgs(String mode) {
    return "'-executeMethod','AddressablesDelivery.Editor.CiBuild.Addressables','-deliveryProfile','CCD','-deliveryMode','${mode}'"
}

def playerArgs() {
    return "'-executeMethod','AddressablesDelivery.Editor.CiBuild.Player'"
}

// 업로드 뒤 공개 주소의 카탈로그 해시가 올린 것과 같은지까지 확인한다(원격 검증).
def uploadArgs(String badge) {
    return "'-executeMethod','AddressablesDelivery.Editor.CiBuild.Upload','-deliveryBadge','${badge}','-deliveryNotes','Jenkins #${env.BUILD_NUMBER} ${badge}'"
}

// 원격 콘텐츠 폴더만 main(v1) 또는 content/v2 로 맞춘다. 브랜치는 바꾸지 않는다(SPEC 5.7).
def useRemoteContent(String source) {
    if (source == 'content/v2') {
        powershell '''
            git fetch --no-tags origin '+refs/heads/content/v2:refs/remotes/origin/content/v2'
            git restore --source=origin/content/v2 -- Assets/LobbyFlowContent/Remote
            exit $LASTEXITCODE
        '''
    } else {
        powershell '''
            git restore -- Assets/LobbyFlowContent/Remote
            git clean -fdq -- Assets/LobbyFlowContent/Remote
            exit $LASTEXITCODE
        '''
    }
}

def withCcdKeys(Closure body) {
    withCredentials([
        string(credentialsId: 'ugs-service-key-id', variable: 'UGS_CLI_SERVICE_KEY_ID'),
        string(credentialsId: 'ugs-service-secret-key', variable: 'UGS_CLI_SERVICE_SECRET_KEY'),
    ]) {
        body()
    }
}

def archivePlayer(String target) {
    if (target == 'Android') {
        archiveArtifacts artifacts: 'Builds/Android/*.apk', fingerprint: true
    } else {
        powershell "Compress-Archive -Path Builds/Windows/* -DestinationPath ${env.CI_LOGS}/LobbyFlow-Windows.zip -Force"
        archiveArtifacts artifacts: "${env.CI_LOGS}/LobbyFlow-Windows.zip", fingerprint: true
    }
}

// Addressables 가 업데이트 기준 파일을 두는 플랫폼 폴더 이름(Assets/AddressableAssetsData/<폴더>).
def platformFolder(String target) {
    return target == 'Android' ? 'Android' : 'Windows'
}
