using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;

        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        [Header("Custom Interaction Inputs")]
        public bool interact; // Sol Tık / E
        public bool secondaryInteract; // Sağ Tık (Peek)
        public bool cancel; // F tuşu (Çıkış)
        public bool toggleNotebook; // Tab tuşu
        public bool pause; // Escape tuşu
        public Vector2 scroll; // Fare tekerleği
#if ENABLE_INPUT_SYSTEM
        public void OnMove(InputValue value)
        {
            MoveInput(value.Get<Vector2>());
        }

        public void OnLook(InputValue value)
        {
            if (cursorInputForLook)
            {
                LookInput(value.Get<Vector2>());
            }
        }

        public void OnJump(InputValue value)
        {
            JumpInput(value.isPressed);
        }

        public void OnSprint(InputValue value)
        {
            SprintInput(value.isPressed);
        }
#endif

        public void MoveInput(Vector2 newMoveDirection)
        {
            move = newMoveDirection;
        }

        public void LookInput(Vector2 newLookDirection)
        {
            look = newLookDirection;
        }

        public void JumpInput(bool newJumpState)
        {
            jump = newJumpState;
        }

        public void SprintInput(bool newSprintState)
        {
            sprint = newSprintState;
        }

        public void OnInteract(InputValue value)
        {
            interact = value.isPressed;
        }

        public void OnSecondaryInteract(InputValue value)
        {
            secondaryInteract = value.isPressed;
        }

        public void OnCancel(InputValue value)
        {
            cancel = value.isPressed;
        }

        public void OnToggleNotebook(InputValue value)
        {
            toggleNotebook = value.isPressed;
        }

        public void OnScroll(InputValue value)
        {
            scroll = value.Get<Vector2>();
        }

        public void OnPause(InputValue value)
        {
            pause = value.isPressed;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                // EĞER OYUNDA BİR GAMEMANAGER VARSA KONTROLÜ ONA BIRAK!
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.UpdateCursorState();
                }
                else
                {
                    // GameManager yoksa (test sahnesi vs) eski yöntemi kullan
                    SetCursorState(cursorLocked);
                }
            }
        }

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
