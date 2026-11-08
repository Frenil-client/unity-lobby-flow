# 저장소 무결성 검사. Unity 를 띄우지 않고 git 과 파일만 본다. 실패 항목을 모두 출력하고 하나라도 있으면 1 로 끝난다.
# Unity 가 필요한 검사(그룹 규칙, 번들 중복, 설정)는 EditMode 테스트가 맡는다.
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

# 업데이트 기준 파일을 커밋하는 플랫폼(Assets/AddressableAssetsData/<폴더>). 아직 첫 전체 빌드를 하지 않은 플랫폼은 뺀다.
$baselineFolders = @({{BASELINE_FOLDERS}})
# 동적 데이터를 비워 저장하는 TMP 폰트 폴더(설정 에셋의 Dynamic Font Folders 와 같게).
$fontFolders = @({{FONT_FOLDERS}})

# 1. 콘텐츠 업데이트 기준 파일이 플랫폼마다 커밋돼 있다.
foreach ($folder in $baselineFolders) {
    $state = "Assets/AddressableAssetsData/$folder/addressables_content_state.bin"
    Check "$folder 업데이트 기준 파일" ((git ls-files $state) -ne $null) $state
}

# 2. JSON 카탈로그 심볼이 대상 플랫폼마다 있다(설정 값만으로는 켜지지 않는다).
$projectSettings = Get-Content 'ProjectSettings/ProjectSettings.asset' -Raw
foreach ($target in 'Standalone', 'Android', 'iPhone') {
    Check "$target ENABLE_JSON_CATALOG" ($projectSettings -match "(?m)^\s+${target}:.*ENABLE_JSON_CATALOG")
}

# 3. 빌드 도중에만 생기는 상태가 커밋되지 않았다. 프로젝트가 preloadedAssets 를 직접 쓰면 이 검사를 지운다.
Check 'preloadedAssets 가 비어 있다' ($projectSettings -match '(?m)^\s+preloadedAssets: \[\]')
Check 'link.xml 을 추적하지 않는다' ((git ls-files 'Assets/AddressableAssetsData/link.xml') -eq $null)

# 4. 동적 폰트 에셋이 비운 상태로 저장돼 있다(폰트 번들 해시 고정).
foreach ($folder in $fontFolders) {
    foreach ($path in @(git ls-files "$folder/*.asset")) {
        $text = Get-Content $path -Raw
        if ($text -notmatch '(?m)^\s+m_AtlasPopulationMode: 1') { continue }
        $empty = ($text -match '(?m)^\s+m_GlyphTable: \[\]') -and ($text -match '(?m)^\s+m_CharacterTable: \[\]')
        Check "동적 폰트 비움: $path" $empty
    }
}

# 5. 셰이더가 D3D11 디버그 정보를 넣지 않는다. 넣으면 컴파일마다 달라지는 디버그 청크 때문에 같은 커밋이라도
#    Windows 번들 해시가 PC 와 셰이더 캐시마다 달라진다(TextMesh Pro 의 TMP_SDF-Mobile 이 기본으로 켜 둔다).
$debugShaders = git grep -lI 'enable_d3d11_debug_symbols' -- 'Assets/*.shader' 'Assets/*.cginc' 'Assets/*.hlsl'
Check '셰이더 D3D11 디버그 정보를 끈다' (@($debugShaders).Count -eq 0) ($debugShaders -join ', ')

# 6. 업로드 키가 저장소에 없다.
$secrets = git grep -nIE '(SERVICE_SECRET_KEY|SERVICE_KEY_ID|AWS_SECRET_ACCESS_KEY)[[:space:]]*[:=][[:space:]]*["'']?[A-Za-z0-9+/=_-]{16,}' -- ':!ci/integrity.ps1'
Check '업로드 키가 커밋되지 않았다' (@($secrets).Count -eq 0) ($secrets -join '; ')

if ($failures.Count -gt 0) {
    Write-Host "무결성 검사 실패 $($failures.Count)건"
    exit 1
}

Write-Host '무결성 검사 통과'
exit 0
