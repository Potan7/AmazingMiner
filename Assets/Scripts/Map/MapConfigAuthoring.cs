using Unity.Entities;
using UnityEngine;

namespace CoreDriller.Map
{
    public class MapConfigAuthoring : MonoBehaviour
    {
        [Header("Materials")]
        [Tooltip("지형 렌더링에 사용되는 기본 머티리얼")]
        public Material terrainMaterial;

        [Tooltip("균열 이펙트 스프라이트 시트가 연결된 머티리얼")]
        public Material damageDecalMaterial;

        [Header("Prefabs")]
        [Tooltip("스프라이트 엔티티 프리팹 (RenderMesh 포함)")]
        [UnityEngine.Serialization.FormerlySerializedAs("damageDecalPrefab")]
        public GameObject spritePrefab;

        [Tooltip("파편 엔티티 프리팹 (지형 머티리얼 적용됨)")]
        public GameObject debrisPrefab;

        class MapConfigAuthoringBaker : Baker<MapConfigAuthoring>
        {
            public override void Bake(MapConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                // 1. Unmanaged Component (Burst 컴파일 가능 - 프리팹 엔티티 저장)
                AddComponent(entity, new MapConfigData
                {
                    SpritePrefab = GetEntity(authoring.spritePrefab, TransformUsageFlags.Dynamic),
                    DebrisPrefab = GetEntity(authoring.debrisPrefab, TransformUsageFlags.Dynamic)
                });

                // 2. Managed Component (렌더링 스레드 전용 - 머티리얼 저장)
                AddComponentObject(entity, new MapConfigMaterials
                {
                    TerrainMaterial = authoring.terrainMaterial,
                    DamageDecalMaterial = authoring.damageDecalMaterial
                });
            }
        }
    }

    // Unmanaged Component (Burst-compatible)
    public struct MapConfigData : IComponentData
    {
        public Entity SpritePrefab;
        public Entity DebrisPrefab;
    }

    // Managed Component (For Materials)
    public class MapConfigMaterials : IComponentData
    {
        public Material TerrainMaterial;
        public Material DamageDecalMaterial;
    }
}
