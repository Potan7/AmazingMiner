using CoreDriller.Player.StatSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemSlot : MonoBehaviour
{
    [SerializeField] Image itemImage;
    [SerializeField] TextMeshProUGUI itemCount;
    [SerializeField] Image lockedImage;

    bool isLocked;

    public void SetLocked(bool IsLocked)
    {
        isLocked = IsLocked;
        lockedImage.gameObject.SetActive(isLocked);
    }

    public void SetItem(InventoryBuffer item)
    {
        if (isLocked || item.ItemType < 1)
        {
            itemImage.gameObject.SetActive(false);
            itemCount.gameObject.SetActive(false);  
            return;
        }   

        // TODO: 아이템 타입에 따른 이미지 설정
        itemCount.SetText("{0}", item.Count);

        itemImage.gameObject.SetActive(true);
        itemCount.gameObject.SetActive(true);
        
    }
}
