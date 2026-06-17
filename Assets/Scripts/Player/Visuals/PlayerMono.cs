using CoreDriller.Player;
using CoreDriller.Player.StatSystem;
using CoreDriller.Motion.Movement;
using Unity.Cinemachine;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using R3;
using Cysharp.Threading.Tasks;

public class PlayerMono : MonoBehaviour
{
    private Entity PlayerEntity => PlayerManager.Instance.PlayerEntity;

    public CinemachineCamera mainCam;
    public Animator playerAnimator;
    public bool isGrounded;

    void Update()
    {
        if (PlayerEntity == Entity.Null) return;

        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var playerTransform = entityManager.GetComponentData<LocalTransform>(PlayerEntity);

        transform.position = new Vector3(playerTransform.Position.x, playerTransform.Position.y, transform.position.z);

        // Grounded 상태를 내부 변수 및 Animator에 동기화
        if (entityManager.HasComponent<PlayerGroundedData>(PlayerEntity))
        {
            isGrounded = entityManager.GetComponentData<PlayerGroundedData>(PlayerEntity).IsGrounded;
        }

        // 각 상태 변수 수집
        bool isMoving = false;
        bool isBoosting = false;
        if (entityManager.HasComponent<MovementInput>(PlayerEntity))
        {
            var moveInput = entityManager.GetComponentData<MovementInput>(PlayerEntity);
            isMoving = Mathf.Abs(moveInput.Direction.x) > 0.01f;
            isBoosting = moveInput.Jump;
        }

        bool isDrilling = false;
        if (entityManager.HasComponent<PlayerDrillData>(PlayerEntity))
        {
            isDrilling = entityManager.GetComponentData<PlayerDrillData>(PlayerEntity).IsActive;
        }

        // Animator 파라미터 업데이트
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("IsMoving", isMoving);
            playerAnimator.SetBool("IsBoosting", isBoosting);
            playerAnimator.SetBool("IsDrilling", isDrilling);
            playerAnimator.SetBool("IsGrounded", isGrounded);
        }

        // 이동 방향에 따라 localScale.x 플립
        if (entityManager.HasComponent<MovementInput>(PlayerEntity))
        {
            float moveX = entityManager.GetComponentData<MovementInput>(PlayerEntity).Direction.x;
            if (moveX > 0.01f)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
            else if (moveX < -0.01f)
            {
                Vector3 scale = transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }
    }

    void Start()
    {
        // Return 키 입력 이벤트 구독
        if (PlayerManager.Instance != null)
        {
            PlayerManager.Instance.OnReturnKeyPerformed
                .Subscribe(_ => OnReturnKey())
                .AddTo(destroyCancellationToken);
        }
    }

    void OnReturnKey()
    {
        if (PlayerEntity != Entity.Null)
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!entityManager.HasComponent<NormalReturnTag>(PlayerEntity))
            {
                entityManager.AddComponent<NormalReturnTag>(PlayerEntity);
            }
        }
    }
}
