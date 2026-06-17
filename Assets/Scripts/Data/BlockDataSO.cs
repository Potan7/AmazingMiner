using UnityEngine;

[System.Serializable]
public class BlockVisualData
{
    public int BlockType;
    public string Name;
    [TextArea(2, 5)]
    public string Description;
    public Sprite Icon;
    public Sprite TerrainSprite; // 지형 타일로 렌더링될 스프라이트 에셋
    public int AtlasIndex;
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
