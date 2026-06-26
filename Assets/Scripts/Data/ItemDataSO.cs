using Unity.Entities;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDataSO", menuName = "ScriptableObjects/ItemDataSO")]
public class ItemDataSO : ScriptableObject
{
    public ItemVisualData[] ItemVisualDatas;
    public ItemSpec[] ItemSpecs;
}

[System.Serializable]
public class ItemVisualData
{
    public int ItemID;
    public string Name;

    [TextArea(2, 5)]
    public string Description;

    public Sprite Icon;
    public Sprite DropSprite;
}

[System.Serializable]
public struct ItemSpec
{
    public int ItemID;
    public int Value;
    public float MaxStackMultiplier;
}

// Blob Asset에 들어갈 Unmanaged 구조체
public struct ItemBlobInfo
{
    public int ItemID;
    public int Value;
    public float MaxStackMultiplier;
}

// 전체 아이템 목록을 담을 루트 Blob 구조체
public struct ItemDatabaseBlob
{
    public BlobArray<ItemBlobInfo> Items;
}

// 이 BlobAsset의 참조(포인터)를 들고 있을 컴포넌트
public struct ItemDatabaseReference : IComponentData
{
    public BlobAssetReference<ItemDatabaseBlob> Reference;
}