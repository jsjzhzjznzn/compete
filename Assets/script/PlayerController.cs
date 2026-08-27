using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float turnSpeed = 10f;

    private CharacterController controller;
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 move = new Vector3(input.x, 0f, input.y);

        if (move != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = 0f;
        }
        velocity.y += gravity * Time.deltaTime;

        controller.Move((move * moveSpeed + new Vector3(0f, velocity.y, 0f)) * Time.deltaTime);
    }
}
