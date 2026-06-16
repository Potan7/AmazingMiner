using CoreDriller.Player.StatSystem;
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
        PlayerUIEvents.OnInventoryChanged += OnInventoryChanged;
        PlayerManager.Instance.OnInventoryKeyPerformed += OnInventoryKeyPerformed;

        if (inventoryPanel.activeSelf) { SetInventoryPanel(false); }
    }

    private void OnDestroy()
    {
        PlayerUIEvents.OnInventoryChanged -= OnInventoryChanged;

        if (PlayerManager.Instance == null) return;
        PlayerManager.Instance.OnInventoryKeyPerformed -= OnInventoryKeyPerformed;
    }

    void OnInventoryKeyPerformed(InputAction.CallbackContext context)
    {
        SetInventoryPanel(!isInventoryPanelActive);
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
