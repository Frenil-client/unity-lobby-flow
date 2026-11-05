# 저장소 무결성 검사(SPEC 8). Unity 를 띄우지 않고 git 과 파일만 본다. 실패 항목을 모두 출력하고 하나라도 있으면 1 로 끝난다.
# Unity 가 필요한 검사(데이터 주소, 라벨, 그룹 규칙)는 EditMode 테스트(ContentIntegrityTests)가 맡는다.
$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]

function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) {
        Write-Host "[통과] $name"
    } else {
        Write-Host "[실패] $name $detail"
        $failures.Add($name)
    }
}

# 1. content/v2 는 main 에서 갈라진 뒤 Remote/ 아래만 바꾼다(SPEC 5.7).
git fetch --no-tags origin '+refs/heads/content/v2:refs/remotes/origin/content/v2' 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    $outside = git diff --name-only 'HEAD...origin/content/v2' | Where-Object { $_ -notlike 'Assets/LobbyFlowContent/Remote/*' }
    Check 'content/v2 는 Remote/ 만 바꾼다' (@($outside).Count -eq 0) ($outside -join ', ')
} else {
    Check 'content/v2 브랜치를 받을 수 있다' $false
}

# 2. 콘텐츠 업데이트 기준 파일이 플랫폼마다 커밋돼 있다.
foreach ($platform in 'Windows', 'Android') {
    $state = "Assets/AddressableAssetsData/$platform/addressables_content_state.bin"
    Check "$platform 업데이트 기준 파일" ((git ls-files $state) -ne $null) $state
}

# 3. JSON 카탈로그 심볼이 대상 플랫폼마다 있다(SPEC 6.2).
$projectSettings = Get-Content 'ProjectSettings/ProjectSettings.asset' -Raw
foreach ($target in 'Standalone', 'Android', 'iPhone') {
    Check "$target ENABLE_JSON_CATALOG" ($projectSettings -match "(?m)^\s+${target}:.*ENABLE_JSON_CATALOG")
}

# 4. 빌드 도중에만 생기는 상태가 커밋되지 않았다.
Check 'preloadedAssets 가 비어 있다' ($projectSettings -match '(?m)^\s+preloadedAssets: \[\]')
Check 'link.xml 을 추적하지 않는다' ((git ls-files 'Assets/AddressableAssetsData/link.xml') -eq $null)

# 5. 동적 폰트 에셋이 비운 상태로 저장돼 있다(로컬 폰트 번들 해시 고정).
$font = Get-Content 'Assets/LobbyFlowContent/Local/Fonts/NanumGothic SDF.asset' -Raw
Check '폰트 에셋 글자 표가 비어 있다' (($font -match '(?m)^\s+m_GlyphTable: \[\]') -and ($font -match '(?m)^\s+m_CharacterTable: \[\]'))

# 6. 생성 도구(빌더)와 비밀값이 저장소에 없다.
Check '빌더 코드를 추적하지 않는다' ((git ls-files 'Assets/LobbyFlow/Builder') -eq $null)
$secrets = git grep -nIE '(SERVICE_SECRET_KEY|SERVICE_KEY_ID)[[:space:]]*[:=][[:space:]]*["'']?[A-Za-z0-9+/=_-]{16,}' -- ':!ci/integrity.ps1'
Check '서비스 계정 키가 커밋되지 않았다' (@($secrets).Count -eq 0) ($secrets -join '; ')

if ($failures.Count -gt 0) {
    Write-Host "무결성 검사 실패 $($failures.Count)건"
    exit 1
}

Write-Host '무결성 검사 통과'
exit 0
