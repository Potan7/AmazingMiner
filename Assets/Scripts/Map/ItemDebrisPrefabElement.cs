using Unity.Entities;

namespace CoreDriller.Map
{
    // ItemID와 해당 아이템 드랍용 프리팹 Entity 매핑을 보관하는 DynamicBuffer용 엘리먼트
    public struct ItemDebrisPrefabElement : IBufferElementData
    {
        public int ItemID;
        public Entity DebrisPrefab;
    }
}
