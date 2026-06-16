# 프로젝트 코어 드릴러 (Project Core Driller) - AI 가이드라인

## 1. 프로젝트 개요 (Project Overview)
**Project Core Driller**는 Unity 6.3 및 ECS(Entities) 1.0+ 아키텍처를 기반으로 하는 2D 횡스크롤 채굴 및 공장 자동화 융합 게임입니다. 플레이어는 지하 탐사(Mining)를 통해 자원을 수집하고, 지상 기지(Persistent Base)에서 이를 가공하여 장비를 업그레이드하는 핵심 루프를 가집니다.

- **핵심 장르:** 로그라이트 채굴 + 팩토리오식 자동화 시뮬레이션
- **주요 기술:** Unity 6.3, Entities (ECS), Burst Compiler, C# Job System, LowLevelPhysics2D, BatchRendererGroup (BRG), Entities Graphics.

## 2. 아키텍처 및 시스템 설계 (Architecture)

### 2.1. 이중 월드 구조 (Dual World System)
- **PersistentWorld (지상):** 영구적인 공장 설비, 전력망, 창고 데이터를 관리합니다. 플레이어가 사망해도 유지됩니다.
- **VolatileWorld (지하):** 절차적으로 생성되는 탐사 구역입니다. 귀환 또는 사망 시 월드 전체가 `Dispose`되어 초기화됩니다.

### 2.2. 지형 시스템 (Terrain System)
- **청크 기반:** 16x16 크기의 블록들로 구성된 청크 단위로 관리됩니다 (`ChunkComponent`).
- **데이터 저장:** `DynamicBuffer<BlockBuffer>`를 사용하여 각 엔티티에 블록 데이터를 저장합니다.
- **렌더링:** Marching Squares 대신, 블록이 있는 자리에만 1x1 쿼드를 그리는 방식의 `BlockMeshJob`을 사용하며, BatchRendererGroup(BRG)을 통해 최적화합니다.

### 2.3. 물리 및 자동화 (Physics & Automation)
- **LowLevelPhysics2D:** 수만 개의 파편 및 액체 물리 연산을 위해 커스텀 물리 엔진 레이어를 직접 호출합니다.
- **물류 시스템:** 컨베이어 벨트, 투입기(Inserter)는 틱 단위로 동기화되며, ECS의 비정규화(Denormalization) 기법을 사용하여 참조 오버헤드를 최소화합니다.

## 3. ECS 코딩 절대 규칙 (Strict ECS Rules)

1. **최신 API 준수:** `Entities.ForEach` 대신 메인 스레드에서는 `SystemAPI.Query<T>()`와 `foreach` 루프를 사용합니다.
2. **성능 최적화:** 모든 무거운 연산은 `IJobEntity` 및 `[BurstCompile]`을 적용하여 워커 스레드에서 처리합니다.
3. **메모리 관리:** `OnUpdate`나 Job 내부에서 Managed 객체(`new class()`) 할당을 금지합니다. `NativeArray`, `DynamicBuffer`를 사용하고 적절한 시점에 `Dispose`합니다.
4. **구조체 중심:** 컴포넌트는 `struct` 및 `IComponentData`로 작성합니다. Managed 컴포넌트는 필요한 경우에만 제한적으로 허용합니다.
5. **Low-Level 접근:** 성능이 임계치에 도달할 경우 `BatchRendererGroup`이나 `LowLevelPhysics2D`와 같은 저수준 API를 적극 활용합니다.
6. **버전 업데이트와 최신 API 유지:** 본 프로젝트는 지속적으로 Unity 엔진 및 Entities 패키지 버전을 최신으로 업데이트합니다. 따라서 `RenderMesh`와 같이 향후 삭제될(Obsolete) 레거시 API의 사용을 지양하고, `RenderMeshArray`나 `RenderMeshUnmanaged` 등 최신 DOTS/Entities 표준 스택으로 대체하여 구현해야 합니다.


## 4. 개발 및 빌드 컨벤션 (Conventions)

- **블록 및 아이템 ID 명명 규칙 (ID Naming Conventions)**:
  - `1 ~ 1000`: 기본 지형 블록 (Bedrock, Dirt, Stone 등)
  - `1001 ~ 2000`: 광석 (Coal, Iron, Copper, Gold 등)
  - `2001 ~`: 공장 생산 아이템 및 기계 부품
- **검증 프로토콜:** 모든 버그 수정 시, 다시 코드를 리뷰하며 구현 후에는 ECS 데이터 무결성 및 메모리 누수 여부를 체크합니다.
- **주석 및 문서화:** 코드 변경 이유와 핵심 로직에 대한 간결한 주석을 포함합니다.

## 5. 현재 프로젝트 상태 (Current State)
- **지형 생성:** Perlin Noise 기반 16x16 `BlockBuffer` 청크 생성 및 베드락 테두리 구현 완료.
- **렌더링:** `BlockMeshJob` 기반의 쿼드 메쉬 생성 및 렌더링 파이프라인 구축.
- **진행 중:** 1x8 크랙 스프라이트 시트 기반 Dynamic Damage Decal 시스템 및 DOTS Hybrid Instancing 최적 렌더링(Draw Call 1회 통폐합) 완료.

## 6. Unity CLI 사용 및 검증 가이드 (Unity CLI & Verification Guide)
본 프로젝트는 **`unity-cli`** 도구를 통해 터미널 상에서 유니티 에디터를 완벽히 제어하고 컴파일 무결성을 보장합니다. 향후 모든 작업자는 변경 사항 반영 후 다음 검증 프로토콜을 필수로 준수해야 합니다.

### 6.1. 상태 및 컴파일 무결성 검증
* **상태 확인:** `unity-cli status` 명령어를 통해 에디터 연결 상태가 `ready`인지 확인합니다.
* **코드 컴파일 검증:** C# 코드 수정 후 반드시 `unity-cli editor refresh --compile`을 실행하여 빌드 오류가 없는지 검증합니다.
* **로그 확인:** 컴파일 에러 발생 시 `unity-cli console --type error --lines 10` 명령으로 신속히 콘솔 컴파일 에러를 수집하고 자가 진단합니다.

---
**주의:** 본 파일은 프로젝트의 헌법과 같으므로, 모든 제안과 코드는 위 가이드라인을 엄격히 준수해야 합니다.
