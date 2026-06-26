using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    [System.Serializable]
    public class BlockVisualData
    {
        public int BlockType;
        public string Name;
        [TextArea(2, 5)]
        public string Description;
        public Sprite Icon;
        public Sprite TerrainSprite; // 지형 타일로 렌더링될 단일 스프라이트 (Fallback)
        public List<Sprite> TerrainSprites = new List<Sprite>(); // 지형 타일용 랜덤 스프라이트 리스트
        public int DropItemID; // 채굴 시 드롭될 아이템 ID (0이면 드롭 없음)
    }

    [System.Serializable]
    public struct BlockSpec
    {
        public int BlockType;
        public float Hardness;
        public float MiningTime;
        public float MaxHP;
        public int DropItemID;
    }

    [CreateAssetMenu(fileName = "BlockDataSO", menuName = "ScriptableObjects/BlockDataSO")]
    public class BlockDataSO : ScriptableObject
    {
        public BlockVisualData[] BlockVisualDatas;
        public BlockSpec[] BlockSpecs;
    }
}