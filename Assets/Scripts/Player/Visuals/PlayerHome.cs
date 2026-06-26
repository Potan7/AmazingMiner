using UnityEngine;
using CoreDriller.Player.StatSystem;

namespace CoreDriller.Player.Visuals
{
    /// <summary>
    /// 기지 씬(PersistentWorld) 전용 플레이어 컨트롤러.
    /// Rigidbody2D + CapsuleCollider2D로 일반 Unity 물리를 사용하며,
    /// PlayerMono(비주얼)를 자식으로 붙여 애니메이션을 구동한다.
    /// ECS Player Entity와 무관하게 동작한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerHome : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Visual")]
        [SerializeField] private PlayerMono playerMono;

        // Animator 파라미터 해시 (PlayerMono와 동일한 키 사용)
        private int IsGroundedHash => PlayerMono.IsGroundedHash;
        private int IsMovingHash => PlayerMono.IsMovingHash;

        private Rigidbody2D _rb;
        private bool _isGrounded;

        // -------------------------------------------------------

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        void FixedUpdate()
        {
            if (PlayerManager.Instance == null) return;

            var input = PlayerManager.Instance.MoveInput;

            // 착지 판정 (OverlapCircle)
            _isGrounded = groundCheck != null &&
                          Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            // 좌우 이동 (수직 속도는 유지하여 중력 자연 적용)
            _rb.linearVelocity = new Vector2(input.x * moveSpeed, _rb.linearVelocity.y);

            // 방향 전환 — localScale.x 플립
            if (input.x > 0.01f)
                transform.localScale = new Vector3(1f, 1f, 1f);
            else if (input.x < -0.01f)
                transform.localScale = new Vector3(-1f, 1f, 1f);

            // 점프 적용
            if (PlayerManager.Instance.JumpInput && _isGrounded)
            {
                _rb.AddForce(Vector2.up * 5f, ForceMode2D.Impulse);
            }
        }

        void Update()
        {
            UpdateAnimation();
        }

        // -------------------------------------------------------

        private void UpdateAnimation()
        {
            if (playerMono == null || playerMono.playerAnimator == null) return;

            var input    = PlayerManager.Instance != null ? PlayerManager.Instance.MoveInput : Vector2.zero;
            bool isMoving = Mathf.Abs(input.x) > 0.01f;

            playerMono.playerAnimator.SetBool(IsGroundedHash, _isGrounded);
            playerMono.playerAnimator.SetBool(IsMovingHash,   isMoving);
            // 기지 씬에서 IsBoosting / IsDrilling은 항상 false (해당 액션 없음)
        }

        // -------------------------------------------------------

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
#endif
    }
}
