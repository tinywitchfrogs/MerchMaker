using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArtSimulator;

public class SendProduct : MonoBehaviour
{
    private static SendProduct instance;
    public static SendProduct Instance
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<SendProduct>();
            return instance;
        }
        private set => instance = value;
    }

    [Header("Configuración de Envío")]
    [Tooltip("Precio por unidad pagado si se entrega una copia sin pedido activo")]
    [SerializeField] private int basePriceFallback = 1000;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryProcessDeliveredCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryProcessDeliveredCollider(other);
    }

    private void TryProcessDeliveredCollider(Collider other)
    {
        if (other == null) return;

        // Evitar procesar un objeto que aún está sostenido por el jugador (Desktop o VR)
        if (FirstPersonPlayerController.Instance != null && 
            FirstPersonPlayerController.Instance.IsHoldingObject(other.transform))
        {
            return;
        }

        var grab = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected)
        {
            return;
        }

        ProcessDeliveredObject(other.gameObject);
    }

    /// <summary>
    /// Procesa la entrega de un producto / dibujo físico colocado en la caja de envíos.
    /// Retorna true si el objeto fue procesado y consumido con éxito.
    /// </summary>
    public bool ProcessDeliveredObject(GameObject obj)
    {
        if (obj == null) return false;

        ScannableDrawing drawing = obj.GetComponent<ScannableDrawing>();
        if (drawing == null) drawing = obj.GetComponentInParent<ScannableDrawing>();

        // Si no tiene ScannableDrawing ni tag Dibujo, no es un producto entregable
        if (drawing == null && !obj.CompareTag("Dibujo"))
        {
            return false;
        }

        // Si es la ilustración ORIGINAL, no la destruimos para evitar que el jugador pierda su arte
        if (drawing != null && drawing.IsOriginal)
        {
            Debug.LogWarning("<color=yellow>[Caja de Envíos]</color> Has colocado el dibujo ORIGINAL. ¡Solo debes enviar las copias impresas!");
            return false;
        }

        string drawingTitle = "";
        if (drawing != null)
        {
            drawingTitle = drawing.GetArtworkData().title;
        }
        else
        {
            drawingTitle = obj.name;
        }

        // Limpiar prefijos del nombre de la copia
        string cleanTitle = drawingTitle
            .Replace("Copia - ", "")
            .Replace("Dibujo_Copia_", "")
            .Trim();

        // Buscar pedido activo en la Tableta
        OrderItem targetOrder = FindMatchingOrder(cleanTitle);

        if (targetOrder != null)
        {
            targetOrder.unitsDelivered++;
            if (targetOrder.product != null)
            {
                targetOrder.product.totalUnitsSold++;
            }
            Debug.Log($"<color=cyan>[Caja de Envíos]</color> Unidad entregada ({targetOrder.unitsDelivered}/{targetOrder.unitsRequested}) para el pedido <b>{targetOrder.orderId}</b> ({targetOrder.product.title}).");

            // Si se completaron todas las unidades requeridas
            if (targetOrder.unitsDelivered >= targetOrder.unitsRequested)
            {
                targetOrder.isCompleted = true;

                // 1. Eliminar/Borrar de la lista de pedidos en la tableta
                if (TabletManager.Instance != null)
                {
                    TabletManager.Instance.CompleteOrder(targetOrder);
                }

                // 2. Recompensa económica (precio del producto * unidades requeridas)
                int totalReward = targetOrder.product != null && targetOrder.product.price > 0
                    ? targetOrder.product.price * targetOrder.unitsRequested
                    : basePriceFallback * targetOrder.unitsRequested;

                if (PlayerEconomy.Instance != null)
                {
                    PlayerEconomy.Instance.AgregarDinero(totalReward);
                }
                else
                {
                    var econ = FindFirstObjectByType<PlayerEconomy>();
                    if (econ != null) econ.AgregarDinero(totalReward);
                }

                // 3. Efecto de sonido ka-ching de venta exitosa
                SoundEffectManager.Play("ka-ching");
                Debug.Log($"<color=green>[Caja de Envíos]</color> 🎉 ¡PEDIDO <b>{targetOrder.orderId}</b> COMPLETADO! Marcado y borrado de la lista. Ganaste: <b>${totalReward}</b>");
            }
            else
            {
                // Aún faltan unidades para completar el pedido
                if (TabletManager.Instance != null)
                {
                    TabletManager.Instance.UpdateOrderProgress(targetOrder);
                }

                SoundEffectManager.Play("ka-ching");
                Debug.Log($"<color=yellow>[Caja de Envíos]</color> Faltan {targetOrder.unitsRequested - targetOrder.unitsDelivered} unidad(es) para completar el pedido {targetOrder.orderId}.");
            }
        }
        else
        {
            // Venta libre de mercancía si no hay un pedido activo pendiente
            int freeReward = basePriceFallback;
            if (PlayerEconomy.Instance != null)
            {
                PlayerEconomy.Instance.AgregarDinero(freeReward);
            }
            else
            {
                var econ = FindFirstObjectByType<PlayerEconomy>();
                if (econ != null) econ.AgregarDinero(freeReward);
            }

            SoundEffectManager.Play("ka-ching");
            Debug.Log($"<color=green>[Caja de Envíos]</color> Copia física vendida (sin pedido asignado). +${freeReward}.");
        }

        // Si el jugador lo tenía en sus manos, liberar la referencia
        if (FirstPersonPlayerController.Instance != null)
        {
            FirstPersonPlayerController.Instance.ReleaseDeliveredObject(obj.transform);
        }

        // Destruir la copia entregada
        Destroy(obj);
        return true;
    }

    private OrderItem FindMatchingOrder(string cleanTitle)
    {
        if (TabletManager.Instance == null) return null;
        IReadOnlyList<OrderItem> orders = TabletManager.Instance.CurrentOrders;
        if (orders == null || orders.Count == 0) return null;

        // 1. Prioridad: Coincidencia de título con el producto
        for (int i = 0; i < orders.Count; i++)
        {
            OrderItem ord = orders[i];
            if (ord.isCompleted || ord.unitsDelivered >= ord.unitsRequested) continue;

            if (ord.product != null && !string.IsNullOrEmpty(ord.product.title))
            {
                string prodTitle = ord.product.title.Trim();
                if (cleanTitle.Equals(prodTitle, StringComparison.OrdinalIgnoreCase) ||
                    cleanTitle.Contains(prodTitle, StringComparison.OrdinalIgnoreCase) ||
                    prodTitle.Contains(cleanTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return ord;
                }
            }
        }

        // 2. Si no coincide el título exacto, asignar al pedido pendiente más antiguo para fluidez de juego
        for (int i = 0; i < orders.Count; i++)
        {
            OrderItem ord = orders[i];
            if (!ord.isCompleted && ord.unitsDelivered < ord.unitsRequested)
            {
                return ord;
            }
        }

        return null;
    }
}
