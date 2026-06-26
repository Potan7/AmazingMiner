using CoreDriller.Player.StatSystem;
using Cysharp.Threading.Tasks;
using Potan.CoreUtils;
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
            .Subscribe( this, (_, t) => t.OnInventoryChanged())
            .AddTo(destroyCancellationToken);

        // event Action → R3 Subject 구독
        PlayerManager.Instance.OnInventoryKeyPerformed
            .Subscribe(this, (_, t) => t.SetInventoryPanel(!isInventoryPanelActive))
            .AddTo(destroyCancellationToken);

        if (inventoryPanel.activeSelf) { SetInventoryPanel(false); }
    }


    public void SetInventoryPanel(bool isActive)
    {
        isInventoryPanelActive = isActive;
        inventoryPanel.SetActive(isActive);
    }

    private void OnInventoryChanged()
    {
        if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            return;
        }

        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var playerEntity = PlayerManager.Instance != null ? PlayerManager.Instance.PlayerEntity : Entity.Null;

        if (playerEntity == Entity.Null || !entityManager.Exists(playerEntity) || !entityManager.HasComponent<InventoryBuffer>(playerEntity))
        {
            return;
        }

        var invBuffer = entityManager.GetBuffer<InventoryBuffer>(playerEntity);

        for (var i = 0; i < itemSlots.Length; i++)
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
                DevLog.LogError($"Inventory buffer does not have enough slots for item slot {i}. Inventory buffer length: {invBuffer.Length}");
                break;
            }
        }
    }
}
