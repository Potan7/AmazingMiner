# Unity CLI (unity-cli) 종합 레퍼런스 가이드

이 문서는 본 프로젝트의 개발 및 빌드 자동화, 스크립트 컴파일 무결성 검증, 에디터 런타임 제어에 활용되는 **`unity-cli`** 도구의 종합 사용 설명서입니다.

---

## 1. 개요 (Overview)
`unity-cli`는 유니티 에디터의 `InitializeOnLoad` 시점에 기동되는 백그라운드 소켓 커넥터 패키지(포트 `8090`)와 로컬 명령줄(CLI) 도구 간의 고속 IPC 통신을 기반으로 작동합니다. 
터미널에서 명령어 한 줄로 에셋 갱신, 컴파일 체크, 플레이 모드 트리거, 동적 C# 스크립트 실행 등을 수행할 수 있어 빌드 무결성을 검증하고 생산성을 비약적으로 높여 줍니다.

---

## 2. 상태 모니터링 및 제어 (Status & System Control)

### 에디터 연결 상태 체크
유니티 에디터와 커넥터가 활성화되어 명령을 수신할 준비가 되었는지 검사합니다.
```bash
unity-cli status
```
* **출력 예시:**
  ```text
  Unity (port 8090): ready
    Project: D:/Unity/Projects/AmazingMiner
    Version: 6000.3.10f1
    Connector: 0.3.19
    PID:     34260
  ```

---

## 3. 에셋 리프레시 및 스크립트 컴파일 (Assets & Compilation)

### 에셋 데이터베이스 갱신 & 스크립트 재컴파일
코드 또는 어셈블리 정의 파일(`.asmdef`) 변경 시, 강제로 에셋을 리프레시하고 C# 코드를 다시 빌드합니다. 컴파일 결과가 반환될 때까지 블로킹되므로 빌드 성공 검증에 최적화되어 있습니다.
```bash
unity-cli editor refresh --compile
```

### 강제 리프레시
```bash
unity-cli editor refresh --force
```

---

## 4. 플레이 모드 제어 (Play Mode Control)

### 플레이 모드 진입 (진입 완료 시까지 대기)
```bash
unity-cli editor play --wait
```

### 플레이 모드 종료 (정지)
```bash
unity-cli editor stop
```

### 일시 정지 / 재개 토글
```bash
unity-cli editor pause
```

---

## 5. 유니티 콘솔 및 오류 진단 (Unity Console Logs)

### 전체 콘솔 로그 실시간 출력 (기본 최신 순)
```bash
unity-cli console
```

### 에러 및 경고 로그만 최근 20줄 수집
```bash
unity-cli console --type error,warning --lines 20
```

### 풀 스택 트레이스 표시
```bash
unity-cli console --stacktrace full
```

### 에디터 콘솔 로그 비우기
```bash
unity-cli console --clear
```

---

## 6. 동적 C# 코드 런타임 실행 (Execute C# Code)

유니티 에디터가 동작 중인 런타임 환경 내부에서 동적으로 C# 코드를 주입하고, 그 결과값을 문자열로 수집하여 CLI 터미널에 곧바로 출력해 줍니다. 샌드박스 상태 점검 및 런타임 디버깅 시 극도의 강력함을 자랑합니다.

### 6.1. 기본 C# 표현식 실행
```bash
unity-cli exec "UnityEngine.Time.time"
```

### 6.2. 씬 내 게임 오브젝트 탐색 및 상태 룩업
```bash
unity-cli exec "UnityEngine.GameObject.Find(\"Player\").transform.position"
```

### 6.3. 긴 스크립트 코드 파이프라인 전달 (Shell Escaping 우회)
따옴표 이스케이프가 복잡한 여러 줄의 C# 코드를 실행할 때는 표준 입력(stdin) 파이프를 통해 전달합니다.
```bash
echo '
var player = UnityEngine.GameObject.Find("Player");
if (player != null) {
    return $"Player Position: {player.transform.position}";
}
return "Player not found";
' | unity-cli exec
```

### 6.4. 커스텀 Using 지시문 지정 실행
```bash
unity-cli exec "var mesh = new Mesh(); return mesh.name;" --usings UnityEngine
```

---

## 7. 유닛 테스트 구동 (Test Runner)

### EditMode 테스트 일괄 수행
```bash
unity-cli test
```

### PlayMode 테스트 일괄 수행
```bash
unity-cli test --mode PlayMode
```

### 특정 테스트 이름 필터링 구동
```bash
unity-cli test --filter "CoreDriller.Map.Tests"
```

---

## 8. 에디터 뷰 캡처 (Screenshot)

현재 게임의 상태를 시각적으로 기록하고 분석하기 위해 터미널에서 화면 캡처 명령을 내릴 수 있습니다.

### 씬 뷰 캡처
```bash
unity-cli screenshot
```

### 게임 뷰 캡처 및 특정 파일 저장
```bash
unity-cli screenshot --view game --output_path "./ProfilerCaptures/game_capture.png"
```
