using CoreDriller.Player.StatSystem;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryManager : MonoBehaviour
{
    [SerializeField] ItemSlot[] itemSlots;
    [SerializeField] GameObject inventoryPanel;
    bool isInventoryPanelActive = false;

    private void Awake()
    {
        PlayerUIEvents.InventoryChanged
            .Subscribe(_ => OnInventoryChanged())
            .AddTo(destroyCancellationToken);

        // event Action → R3 Subject 구독 (자동 해제)
        PlayerManager.Instance.OnInventoryKeyPerformed
            .Subscribe(_ => SetInventoryPanel(!isInventoryPanelActive))
            .AddTo(destroyCancellationToken);

        if (inventoryPanel.activeSelf) { SetInventoryPanel(false); }
    }


    public void SetInventoryPanel(bool isActive)
    {
        isInventoryPanelActive = isActive;
        inventoryPanel.SetActive(isActive);
    }

    void OnInventoryChanged()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var playerEntity = PlayerManager.Instance.PlayerEntity;
        var invBuffer = entityManager.GetBuffer<InventoryBuffer>(playerEntity);

        for (int i = 0; i < itemSlots.Length; i++)
        {
            if (i >= PlayerManager.Instance.CurrentStats.InventorySlotCount)
            {
                itemSlots[i].SetLocked(true); // Lock the slot if it's beyond the inventory size
                continue;
            }

            if (i < invBuffer.Length)
            {
                itemSlots[i].SetLocked(false);
                itemSlots[i].SetItem(invBuffer[i]);
            }
            else
            {
                Debug.LogError($"Inventory buffer does not have enough slots for item slot {i}. Inventory buffer length: {invBuffer.Length}");
                break;
            }
        }
    }
}
