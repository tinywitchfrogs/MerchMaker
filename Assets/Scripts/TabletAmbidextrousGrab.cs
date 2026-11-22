using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArtSimulator
{
    /// <summary>
    /// Componente de agarre para la tableta que permite tomarla y sostenerla cómodamente
    /// con CUALQUIERA de las dos manos (ambidiestro) en Realidad Virtual.
    /// Detecta dinámicamente si el controlador que interactúa es la mano izquierda o derecha
    /// y utiliza el punto de anclaje (attachTransform) ergonómico correspondiente en el marco lateral.
    /// </summary>
    public class TabletAmbidextrousGrab : XRGrabInteractable
    {
        [Header("Puntos de Agarre por Mano")]
        [Tooltip("Punto de sujeción ergonómico para la mano izquierda (marco lateral izquierdo).")]
        [SerializeField] private Transform leftHandAttachPoint;

        [Tooltip("Punto de sujeción ergonómico para la mano derecha (marco lateral derecho).")]
        [SerializeField] private Transform rightHandAttachPoint;

        public Transform LeftHandAttachPoint
        {
            get => leftHandAttachPoint;
            set => leftHandAttachPoint = value;
        }

        public Transform RightHandAttachPoint
        {
            get => rightHandAttachPoint;
            set => rightHandAttachPoint = value;
        }

        public override Transform GetAttachTransform(IXRInteractor interactor)
        {
            if (interactor != null)
            {
                bool isLeft = IsLeftHand(interactor.transform);
                if (isLeft && leftHandAttachPoint != null)
                {
                    return leftHandAttachPoint;
                }
                if (!isLeft && rightHandAttachPoint != null)
                {
                    return rightHandAttachPoint;
                }
            }

            return base.GetAttachTransform(interactor);
        }

        protected override void OnSelectEntering(SelectEnterEventArgs args)
        {
            if (args.interactorObject != null)
            {
                bool isLeft = IsLeftHand(args.interactorObject.transform);
                if (isLeft && leftHandAttachPoint != null)
                {
                    attachTransform = leftHandAttachPoint;
                }
                else if (!isLeft && rightHandAttachPoint != null)
                {
                    attachTransform = rightHandAttachPoint;
                }
            }

            base.OnSelectEntering(args);
        }

        public static bool IsLeftHand(Transform t)
        {
            while (t != null)
            {
                string name = t.name.ToLower();
                if (name.Contains("left")) return true;
                if (name.Contains("right")) return false;
                t = t.parent;
            }
            return false;
        }
    }
}
