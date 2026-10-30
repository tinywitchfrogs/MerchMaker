using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ArtSimulator
{
    /// <summary>
    /// Proporciona locomoción fluida e inmediata tanto para pruebas en el Editor de Unity (WASD + Ratón)
    /// como para complementar la locomoción nativa de Realidad Virtual (mandos / joysticks).
    /// Permite al usuario caminar, girar y explorar libremente el escenario en cualquier momento.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class XREditorLocomotionHelper : MonoBehaviour
    {
        [Header("Velocidades de Movimiento")]
        [SerializeField] private float walkSpeed = 3.6f;
        [SerializeField] private float sprintSpeed = 6.2f;
        [SerializeField] private float gravity = -18.0f;

        [Header("Control de Vista en Editor")]
        [SerializeField] private float mouseSensitivity = 2.2f;
        [SerializeField] private float snapTurnAngle = 45f;

        private CharacterController characterController;
        private Camera vrCamera;
        private float verticalVelocity = 0f;
        private float cameraPitch = 0f;

        private void Awake()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                enabled = false;
                return;
            }

            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
            }

            // Dimensiones ergonómicas para atravesar puertas y acercarse a escritorios cómodamente
            characterController.height = 1.75f;
            characterController.radius = 0.30f;
            characterController.center = new Vector3(0f, 0.875f, 0f);
            characterController.skinWidth = 0.04f;
            characterController.minMoveDistance = 0f;
            characterController.stepOffset = 0.35f;
            characterController.slopeLimit = 45f;

            FindVRCamera();
        }

        private void Start()
        {
            FindVRCamera();
            if (vrCamera != null)
            {
                cameraPitch = vrCamera.transform.localEulerAngles.x;
                if (cameraPitch > 180f) cameraPitch -= 360f;
            }
        }

        private void FindVRCamera()
        {
            if (vrCamera == null)
            {
                vrCamera = GetComponentInChildren<Camera>();
            }
            if (vrCamera == null)
            {
                vrCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (vrCamera == null) FindVRCamera();

            HandleKeyboardMovement();
            HandleEditorMouseLook();
            HandleSnapTurnKeys();
            HandleEditorTabletInteraction();
        }

        /// <summary>
        /// Movimiento fluido con teclado (WASD / Flechas / Shift) en el espacio horizontal relativo a la cámara.
        /// </summary>
        private void HandleKeyboardMovement()
        {
            float horizontal = 0f;
            float vertical = 0f;
            bool isSprinting = false;

            // Soporte para nuevo Input System
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
                isSprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }

            // Calcular dirección horizontal en base a dónde mira la cámara del jugador
            Vector3 camForward = vrCamera != null ? vrCamera.transform.forward : transform.forward;
            Vector3 camRight = vrCamera != null ? vrCamera.transform.right : transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 wishDir = camForward * vertical + camRight * horizontal;
            if (wishDir.sqrMagnitude > 1f)
            {
                wishDir.Normalize();
            }

            // Gravedad y contacto con el suelo
            if (characterController.isGrounded)
            {
                verticalVelocity = -1.5f; // Mantiene el CharacterController pegado al piso firmemente
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
                if (verticalVelocity < -25f) verticalVelocity = -25f;
            }

            float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;
            Vector3 moveDelta = (wishDir * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime;

            if (moveDelta.sqrMagnitude > 0.000001f)
            {
                characterController.Move(moveDelta);
            }
        }

        /// <summary>
        /// Rotación de vista en Editor al mantener pulsado el Botón Derecho del Ratón (RMB).
        /// </summary>
        private void HandleEditorMouseLook()
        {
            if (!Application.isEditor) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // Si se mantiene presionado el clic derecho del ratón
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * (mouseSensitivity * 0.15f);

                // Yaw: Rotar horizontalmente todo el rig (XR Origin)
                if (Mathf.Abs(delta.x) > 0.01f)
                {
                    transform.Rotate(0f, delta.x, 0f, UnityEngine.Space.World);
                }

                // Pitch: Inclinar verticalmente la cámara local
                if (vrCamera != null && Mathf.Abs(delta.y) > 0.01f)
                {
                    cameraPitch = Mathf.Clamp(cameraPitch - delta.y, -80f, 80f);
                    Transform camParent = vrCamera.transform.parent;
                    if (camParent != null && camParent.name.Contains("Offset"))
                    {
                        // Si hay un Camera Offset, aplicar el pitch suavemente
                        vrCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
                    }
                    else
                    {
                        vrCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
                    }
                }
            }
        }

        /// <summary>
        /// Giro rápido (Snap Turn) con teclas Q y E en el teclado.
        /// </summary>
        private void HandleSnapTurnKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame)
                {
                    transform.Rotate(0f, -snapTurnAngle, 0f, UnityEngine.Space.World);
                }
                else if (keyboard.eKey.wasPressedThisFrame)
                {
                    transform.Rotate(0f, snapTurnAngle, 0f, UnityEngine.Space.World);
                }
            }
        }

        /// <summary>
        /// Permite interactuar y hacer clic directamente en los botones de la tableta en el Editor de Unity
        /// usando el clic izquierdo del ratón o la tecla E / F al apuntar hacia la pantalla.
        /// </summary>
        private void HandleEditorTabletInteraction()
        {
            if (!Application.isEditor) return;

            bool clickTriggered = false;
            Vector2 screenPoint = Vector2.zero;

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            // Clic izquierdo cuando NO se está rotando la cámara con RMB
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.isPressed)
            {
                clickTriggered = true;
                screenPoint = mouse.position.ReadValue();
            }
            else if (keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            {
                clickTriggered = true;
                screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            if (!clickTriggered) return;

            // 1. Raycast estándar UI mediante EventSystem
            var es = EventSystem.current;
            if (es != null)
            {
                var pointerData = new PointerEventData(es) { position = screenPoint };
                var results = new List<RaycastResult>();
                es.RaycastAll(pointerData, results);

                for (int i = 0; i < results.Count; i++)
                {
                    var hitObj = results[i].gameObject;
                    if (hitObj == null) continue;

                    var btn = hitObj.GetComponentInParent<Button>();
                    if (btn != null && btn.interactable)
                    {
                        ExecuteEvents.Execute(btn.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
                        ExecuteEvents.Execute(btn.gameObject, pointerData, ExecuteEvents.submitHandler);
                        btn.onClick.Invoke();
                        Debug.Log($"<color=cyan>[Editor Interaction]</color> Clic ejecutado en botón: <b>{btn.gameObject.name}</b>");
                        return;
                    }

                    var trigger = hitObj.GetComponentInParent<TabletButtonTrigger>();
                    if (trigger != null)
                    {
                        ExecuteEvents.Execute(trigger.gameObject, pointerData, ExecuteEvents.pointerClickHandler);
                        ExecuteEvents.Execute(trigger.gameObject, pointerData, ExecuteEvents.submitHandler);
                        return;
                    }
                }
            }

            // 2. Fallback de proyección directa 3D sobre el Canvas de la tableta
            if (vrCamera != null)
            {
                Ray worldRay = vrCamera.ScreenPointToRay(screenPoint);
                GameObject canvasObj = GameObject.Find("TabletCanvas");
                if (canvasObj != null && canvasObj.activeInHierarchy)
                {
                    Plane canvasPlane = new Plane(canvasObj.transform.forward, canvasObj.transform.position);
                    if (canvasPlane.Raycast(worldRay, out float enter) && enter > 0.05f && enter <= 4.5f)
                    {
                        Vector3 hitWorld = worldRay.GetPoint(enter);
                        Vector2 hitScreen = vrCamera.WorldToScreenPoint(hitWorld);
                        var buttons = canvasObj.GetComponentsInChildren<Button>(false);
                        for (int i = 0; i < buttons.Length; i++)
                        {
                            var b = buttons[i];
                            var rt = b.GetComponent<RectTransform>();
                            if (rt != null && b.interactable && RectTransformUtility.RectangleContainsScreenPoint(rt, hitScreen, vrCamera))
                            {
                                b.onClick.Invoke();
                                Debug.Log($"<color=cyan>[Editor Interaction 3D]</color> Clic ejecutado en botón: <b>{b.gameObject.name}</b>");
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}
