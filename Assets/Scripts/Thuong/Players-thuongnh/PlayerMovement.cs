using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Thuong
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float movementSpeed = 3f;
        public int playerID;
        public bool IsAlive => PlayerManger.Instance == null ||
            (PlayerManger.Instance.GetplayerByID(playerID)?.isAlive ?? false);

        private Rigidbody2D rb;
        private Animator animator;

        private Vector2 moveInput;

        private bool canMove = true;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
        }

        private void FixedUpdate()
        {
            if (!canMove || !IsAlive || (GameRoleManager.Instance != null && GameRoleManager.Instance.currentState != GameState.Day))
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = moveInput * movementSpeed;
        }

        public void Move(InputAction.CallbackContext context)
        {
            if (!canMove)
            {
                moveInput = Vector2.zero;
                return;
            }

            moveInput = Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);

            if (animator != null)
            {
                animator.SetBool("isWalking", moveInput != Vector2.zero);

                if (moveInput != Vector2.zero)
                {
                    animator.SetFloat("LastInputX", moveInput.x);
                    animator.SetFloat("LastInputY", moveInput.y);
                }
            }
        }

        public void SetCanMove(bool value)
        {
            canMove = value;

            if (!canMove)
            {
                moveInput = Vector2.zero;

                if (rb != null)
                    rb.linearVelocity = Vector2.zero;

                if (animator != null)
                    animator.SetBool("isWalking", false);
            }
        }
    
    }
}
