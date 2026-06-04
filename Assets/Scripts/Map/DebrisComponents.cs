using Unity.Entities;
using Unity.Mathematics;

namespace CoreDriller.Map
{
    // 파편 식별을 위한 태그 컴포넌트
    public struct DebrisTag : IComponentData { }

    // 파편의 메타 정보 컴포넌트
    public struct DebrisComponent : IComponentData
    {
        public int ItemType;       // 획득 시 획득될 아이템 종류 (BlockTypes.Coal 등)
        public float SpawnTime;    // 스폰된 게임 시간
        public float Lifetime;     // 디스폰될 때까지의 수명 주기 (초 단위)
    }

    // 파편의 머티리얼이 지형 아틀라스 머티리얼로 교체 완료되었음을 표시하는 태그
    public struct DebrisMaterialInitializedTag : IComponentData { }
}
