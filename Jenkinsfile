// Lobby Flow 빌드 파이프라인(SPEC 8).
// 에이전트: Unity 6000.3.9f1 과 라이선스가 있는 Windows PC(레이블 unity-windows). 설치와 설정은 docs/guides/jenkins-setup.md.
// main 폴링으로 시작된 실행은 무결성 검사와 EditMode 테스트만 한다. 빌드와 CCD 업로드는 사람이 파라미터를 골라 실행한다.
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
        choice(name: 'PLATFORM', choices: ['Android', 'StandaloneWindows64'], description: 'Unity -buildTarget')
        choice(name: 'CONTENT', choices: ['main', 'content/v2'], description: '원격 콘텐츠(Assets/LobbyFlowContent/Remote) 출처. content/v2 는 v2 패치를 만들 때 쓴다')
        choice(name: 'ADDRESSABLES', choices: ['update', 'full', 'none'], description: 'update: 커밋된 기준으로 콘텐츠 업데이트 빌드. full: 기준을 새로 만든다(이미 배포한 플레이어와 호환이 끊긴다)')
        booleanParam(name: 'BUILD_PLAYER', defaultValue: true, description: '개발 빌드(Windows 실행 파일 또는 APK)를 만들어 보관한다')
        booleanParam(name: 'UPLOAD_CCD', defaultValue: false, description: 'CCD 에 올리고 릴리스를 만든다. 실행 중에 승인을 한 번 더 묻는다')
        choice(name: 'CCD_BADGE', choices: ['none', 'v1', 'v2'], description: '새 릴리스에 붙일 콘텐츠 버전 배지(latest 는 CCD 가 옮긴다)')
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
                powershell """
                    ci/unity.ps1 -Name editmode-tests -BuildTarget ${params.PLATFORM} -Arguments '-runTests','-testPlatform','EditMode','-testResults',"\$PWD/ci-logs/editmode-results.xml"
                    exit \$LASTEXITCODE
                """
            }
            post {
                always {
                    nunit testResultsPattern: 'ci-logs/editmode-results.xml', failIfNoResults: false
                }
            }
        }

        stage('원격 콘텐츠 선택') {
            when { allOf { expression { isManualRun() }; expression { params.CONTENT == 'content/v2' } } }
            steps {
                // 브랜치는 바꾸지 않고 Remote/ 만 v2 로 덮는다(SPEC 5.7). 끝나면 post 에서 되돌린다.
                powershell '''
                    git fetch --no-tags origin '+refs/heads/content/v2:refs/remotes/origin/content/v2'
                    git restore --source=origin/content/v2 -- Assets/LobbyFlowContent/Remote
                    exit $LASTEXITCODE
                '''
            }
        }

        stage('Addressables 빌드') {
            when { allOf { expression { isManualRun() }; expression { params.ADDRESSABLES != 'none' } } }
            steps {
                powershell """
                    ci/unity.ps1 -Name addressables -BuildTarget ${params.PLATFORM} -Arguments '-executeMethod','LobbyFlow.Editor.CiBuild.Addressables','-lobbyProfile','CCD','-lobbyMode','${params.ADDRESSABLES}'
                    exit \$LASTEXITCODE
                """
            }
        }

        stage('플레이어 빌드') {
            when { allOf { expression { isManualRun() }; expression { params.BUILD_PLAYER } } }
            steps {
                powershell """
                    ci/unity.ps1 -Name player -BuildTarget ${params.PLATFORM} -Arguments '-executeMethod','LobbyFlow.Editor.CiBuild.Player'
                    exit \$LASTEXITCODE
                """
                script {
                    if (params.PLATFORM == 'Android') {
                        archiveArtifacts artifacts: 'Builds/Android/*.apk', fingerprint: true
                    } else {
                        powershell "Compress-Archive -Path Builds/Windows/* -DestinationPath ${env.CI_LOGS}/LobbyFlow-Windows.zip -Force"
                        archiveArtifacts artifacts: "${env.CI_LOGS}/LobbyFlow-Windows.zip", fingerprint: true
                    }
                }
            }
        }

        stage('CCD 업로드') {
            when { allOf { expression { isManualRun() }; expression { params.UPLOAD_CCD } } }
            steps {
                input message: "CCD(${params.PLATFORM})에 올리고 릴리스를 만들까요? latest 배지를 쓰는 플레이어가 바로 받습니다.", ok: '업로드'
                withCredentials([
                    string(credentialsId: 'ugs-service-key-id', variable: 'UGS_CLI_SERVICE_KEY_ID'),
                    string(credentialsId: 'ugs-service-secret-key', variable: 'UGS_CLI_SERVICE_SECRET_KEY'),
                ]) {
                    // 업로드 뒤 공개 주소의 카탈로그 해시가 올린 것과 같은지까지 확인한다(원격 검증).
                    powershell """
                        ci/unity.ps1 -Name ccd-upload -BuildTarget ${params.PLATFORM} -Arguments '-executeMethod','LobbyFlow.Editor.CiBuild.Upload','-lobbyBadge','${params.CCD_BADGE}','-lobbyNotes','Jenkins #${env.BUILD_NUMBER}'
                        exit \$LASTEXITCODE
                    """
                }
            }
        }
    }

    post {
        always {
            // content/v2 로 덮은 Remote/ 를 되돌려 다음 실행이 main 상태에서 시작하게 한다.
            powershell '''
                git restore -- Assets/LobbyFlowContent/Remote
                git clean -fdq -- Assets/LobbyFlowContent/Remote
            '''
            archiveArtifacts artifacts: 'ci-logs/*.log, ci-logs/*.xml', allowEmptyArchive: true
        }
    }
}

// 사람이 눌러 시작한 실행인지. main 폴링으로 시작된 실행은 검사와 테스트만 한다.
def isManualRun() {
    return currentBuild.getBuildCauses('hudson.triggers.SCMTrigger$SCMTriggerCause').isEmpty()
}
