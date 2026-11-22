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
            if (instance == null) instance = FindAnyObjectByType<SendProduct>();
            return instance;
        }
        private set => instance = value;
    }

    [Header("Configuración de Envío")]
    [Tooltip("Precio por unidad pagado si se entrega un sobre sin pedido activo")]
    [SerializeField] private int basePriceFallback = 1000;

    [Tooltip("Prefab o plantilla de sobre para reponer cuando se envía uno")]
    [SerializeField] private GameObject envelopePrefab;

    private readonly HashSet<GameObject> processedObjects = new HashSet<GameObject>();
    private float lastWarningTime = -10f;

    private float checkTimer = 0f;

    private void Awake()
    {
        if (instance == null) instance = this;
        SetupBoxColliders();
    }

    private void Start()
    {
        SetupBoxColliders();
    }

    private void Update()
    {
        checkTimer += Time.deltaTime;
        if (checkTimer >= 0.25f)
        {
            checkTimer = 0f;
            CheckInsideEnvelopes();
        }
    }

    public Vector3 GetBoxCenter()
    {
        var child = transform.Find("Cube.001") ?? (transform.parent != null ? transform.parent.Find("Cube.001") : null);
        if (child != null)
        {
            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null) return mr.bounds.center;
        }
        var myMr = GetComponent<MeshRenderer>();
        if (myMr != null) return myMr.bounds.center;
        return transform.position;
    }

    public Bounds GetBoxBounds()
    {
        var child = transform.Find("Cube.001") ?? (transform.parent != null ? transform.parent.Find("Cube.001") : null);
        if (child != null)
        {
            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null) return mr.bounds;
        }
        var myMr = GetComponent<MeshRenderer>();
        if (myMr != null) return myMr.bounds;
        return new Bounds(transform.position, Vector3.one * 0.5f);
    }

    public void SetupBoxColliders()
    {
        Transform boxRoot = transform.parent != null && transform.parent.name.Equals("Caja", StringComparison.OrdinalIgnoreCase) 
            ? transform.parent 
            : transform;

        Vector3 center = GetBoxCenter();

        Transform wallsT = boxRoot.Find("BoxPhysicalWalls");
        if (wallsT == null)
        {
            GameObject wGo = new GameObject("BoxPhysicalWalls");
            wGo.transform.SetParent(boxRoot, true);
            wallsT = wGo.transform;
        }
        wallsT.position = center;
        wallsT.rotation = Quaternion.identity;

        // Configurar capa en 'Ignore Raycast' para que el puntero láser de VR pueda interactuar
        // libremente con los sobres u objetos colocados en el interior sin ser bloqueado por las paredes.
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer != -1)
        {
            wallsT.gameObject.layer = ignoreRaycastLayer;
        }

        // Limpiar colisionadores antiguos de paredes si existen
        var oldCols = wallsT.GetComponents<BoxCollider>();
        for (int i = 0; i < oldCols.Length; i++)
        {
            if (Application.isPlaying) Destroy(oldCols[i]);
            else DestroyImmediate(oldCols[i]);
        }

        // Paredes sólidas físicas (Evitan que el sobre o los objetos atraviesen las paredes de cartón)
        // 1. Suelo de la caja
        var bCol = wallsT.gameObject.AddComponent<BoxCollider>();
        bCol.center = new Vector3(0f, -0.30f, 0f);
        bCol.size = new Vector3(0.50f, 0.05f, 0.50f);

        // 2. Pared Izquierda (-X)
        var lCol = wallsT.gameObject.AddComponent<BoxCollider>();
        lCol.center = new Vector3(-0.24f, 0f, 0f);
        lCol.size = new Vector3(0.04f, 0.60f, 0.50f);

        // 3. Pared Derecha (+X)
        var rCol = wallsT.gameObject.AddComponent<BoxCollider>();
        rCol.center = new Vector3(0.24f, 0f, 0f);
        rCol.size = new Vector3(0.04f, 0.60f, 0.50f);

        // 4. Pared Trasera (-Z)
        var bkCol = wallsT.gameObject.AddComponent<BoxCollider>();
        bkCol.center = new Vector3(0f, 0f, -0.24f);
        bkCol.size = new Vector3(0.50f, 0.60f, 0.04f);

        // 5. Pared Delantera (+Z)
        var fCol = wallsT.gameObject.AddComponent<BoxCollider>();
        fCol.center = new Vector3(0f, 0f, 0.24f);
        fCol.size = new Vector3(0.50f, 0.60f, 0.04f);

        // 6. Trigger central de detección de entrega
        var trigCol = GetComponent<BoxCollider>();
        if (trigCol == null) trigCol = gameObject.AddComponent<BoxCollider>();
        trigCol.isTrigger = true;
    }

    /// <summary>
    /// Valida si el sobre cumple los requisitos para ser despachado antes de forzar su liberación de la mano.
    /// </summary>
    public bool CanDeliverEnvelope(EnvelopeController env, out string failureReason)
    {
        failureReason = null;
        if (env == null)
        {
            failureReason = "Sobre inválido";
            return false;
        }

        if (!env.IsSealed)
        {
            failureReason = "⚠️ ¡El sobre debe estar cerrado y sellado antes de despacharlo!";
            return false;
        }

        // Si el tutorial está activo, se acepta de inmediato para no truncar la experiencia del jugador
        bool isTutorialActive = WorkshopTutorialManager.Instance != null && !WorkshopTutorialManager.Instance.IsCompleted;
        if (isTutorialActive)
        {
            return true;
        }

        // En juego normal, verificar pedido activo y copias requeridas
        string cleanTitle = (env.ArtworkTitle ?? "")
            .Replace("Copia - ", "")
            .Replace("Dibujo_Copia_", "")
            .Trim();

        OrderItem targetOrder = FindMatchingOrder(cleanTitle);
        if (targetOrder != null && env.CurrentCopiesCount < targetOrder.unitsRequested)
        {
            failureReason = $"⚠️ El pedido requiere {targetOrder.unitsRequested} copias (el sobre tiene {env.CurrentCopiesCount}). Introduce más copias antes de enviar.";
            return false;
        }

        return true;
    }

    private void CheckInsideEnvelopes()
    {
        Bounds b = GetBoxBounds();
        var envelopes = FindObjectsByType<EnvelopeController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < envelopes.Length; i++)
        {
            var env = envelopes[i];
            if (env == null || processedObjects.Contains(env.gameObject)) continue;

            if (b.Contains(env.transform.position) || Vector3.Distance(b.center, env.transform.position) < 0.35f)
            {
                // Solo liberar y procesar si el sobre realmente cumple las condiciones de entrega
                if (CanDeliverEnvelope(env, out _))
                {
                    ReleaseAndDeliverEnvelope(env);
                    break;
                }
            }
        }
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

        GameObject targetObj = other.gameObject;

        // Proteger rigurosamente elementos del taller y jugador
        if (targetObj.GetComponentInParent<PrinterController>() != null || targetObj.GetComponentInChildren<PrinterController>() != null) return;
        if (targetObj.name.ToLower().Contains("impresora") || targetObj.name.ToLower().Contains("printer")) return;
        if (targetObj.name.ToLower().Contains("tijera") || targetObj.name.ToLower().Contains("mesa")) return;
        if (targetObj.name.ToLower().Contains("atril")) return;
        if (targetObj.GetComponentInParent<TabletManager>() != null || targetObj.GetComponentInChildren<TabletManager>() != null) return;
        if (targetObj.GetComponentInParent<VRPenTip>() != null || targetObj.GetComponentInChildren<VRPenTip>() != null) return;
        if (targetObj.CompareTag("Player") || targetObj.name.Contains("Origin") || targetObj.name.Contains("XR") || targetObj.name.Contains("Player")) return;

        // 1. Verificar si es un Sobre de Envíos
        EnvelopeController envelope = targetObj.GetComponent<EnvelopeController>() ?? targetObj.GetComponentInParent<EnvelopeController>();
        if (envelope != null)
        {
            targetObj = envelope.gameObject;
            if (processedObjects.Contains(targetObj)) return;

            if (!CanDeliverEnvelope(envelope, out string reason))
            {
                if (Time.time - lastWarningTime > 2.5f)
                {
                    lastWarningTime = Time.time;
                    ShowFloatingWarning(reason);
                    Debug.LogWarning($"<color=yellow>[Caja de Envíos]</color> {reason}");
                }
                return;
            }

            // Sobre válido y sellado: procesar entrega
            ReleaseAndDeliverEnvelope(envelope);
            return;
        }

        // 2. Si el jugador coloca hojas sueltas directamente en la caja
        ScannableDrawing drawing = other.GetComponent<ScannableDrawing>() ?? other.GetComponentInParent<ScannableDrawing>();
        if (drawing != null)
        {
            if (drawing.IsOriginal)
            {
                if (Time.time - lastWarningTime > 2.5f)
                {
                    lastWarningTime = Time.time;
                    ShowFloatingWarning("⚠️ ¡No envíes tu dibujo ORIGINAL! Solo debes enviar las copias.");
                    Debug.LogWarning("<color=yellow>[Caja de Envíos]</color> ⚠️ Has colocado el dibujo ORIGINAL. ¡No debes enviar tu obra original, solo las copias!");
                }
                return;
            }
            else
            {
                if (Time.time - lastWarningTime > 2.5f)
                {
                    lastWarningTime = Time.time;
                    ShowFloatingWarning("⚠️ ¡Introduce las copias dentro de un SOBRE antes de despacharlas!");
                    Debug.LogWarning("<color=yellow>[Caja de Envíos]</color> ⚠️ ¡Debes introducir las copias impresas dentro de un <b>Sobre</b> y cerrarlo antes de enviarlas!");
                }
                return;
            }
        }
    }

    private void ReleaseAndDeliverEnvelope(EnvelopeController envelope)
    {
        if (envelope == null || processedObjects.Contains(envelope.gameObject)) return;
        processedObjects.Add(envelope.gameObject);

        // Liberar de la mano VR del jugador
        var grab = envelope.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && grab.isSelected && grab.interactionManager != null)
        {
            var interactor = grab.firstInteractorSelecting;
            if (interactor != null)
            {
                grab.interactionManager.SelectExit(interactor, grab);
            }
        }

        // Liberar de la mano PC del jugador
        if (FirstPersonPlayerController.Instance != null && FirstPersonPlayerController.Instance.IsHoldingObject(envelope.transform))
        {
            FirstPersonPlayerController.Instance.ReleaseDeliveredObject(envelope.transform);
        }

        ProcessDeliveredEnvelope(envelope);
    }

    public void ShowFloatingWarning(string message)
    {
        Vector3 spawnPos = GetBoxCenter() + Vector3.up * 0.40f;
        GameObject warnObj = new GameObject("DeliveryWarning");
        warnObj.transform.position = spawnPos;

        var tm = warnObj.AddComponent<TextMesh>();
        tm.text = message;
        tm.fontSize = 28;
        tm.characterSize = 0.045f;
        tm.fontStyle = FontStyle.Bold;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1.0f, 0.40f, 0.15f);

        warnObj.AddComponent<FloatingWarningFade>();
    }

    /// <summary>
    /// Método público para procesar entrega manual desde FirstPersonPlayerController o VR.
    /// </summary>
    public bool ProcessDeliveredObject(GameObject obj)
    {
        if (obj == null) return false;

        var envelope = obj.GetComponent<EnvelopeController>() ?? obj.GetComponentInParent<EnvelopeController>();
        if (envelope != null)
        {
            if (!envelope.IsSealed)
            {
                Debug.LogWarning("<color=yellow>[Caja de Envíos]</color> ⚠️ ¡El sobre aún no está cerrado! Toca 'Cerrar Sobre' antes de colocarlo en la caja de envíos.");
                return false;
            }
            return ProcessDeliveredEnvelope(envelope);
        }

        var drawing = obj.GetComponent<ScannableDrawing>() ?? obj.GetComponentInParent<ScannableDrawing>();
        if (drawing != null)
        {
            if (drawing.IsOriginal)
            {
                Debug.LogWarning("<color=yellow>[Caja de Envíos]</color> ⚠️ Has colocado el dibujo ORIGINAL. ¡No debes enviar tu obra original, solo las copias!");
            }
            else
            {
                Debug.LogWarning("<color=yellow>[Caja de Envíos]</color> ⚠️ ¡Debes introducir las copias impresas dentro de un <b>Sobre</b> y cerrarlo antes de enviarlas!");
            }
            return false;
        }

        return false;
    }

    /// <summary>
    /// Procesa la entrega de un sobre cerrado con las copias del pedido.
    /// </summary>
    public bool ProcessDeliveredEnvelope(EnvelopeController envelope)
    {
        if (envelope == null || !envelope.IsSealed) return false;

        GameObject envelopeObj = envelope.gameObject;
        string drawingTitle = envelope.ArtworkTitle;
        int copiesInEnvelope = envelope.CurrentCopiesCount;

        string cleanTitle = drawingTitle
            .Replace("Copia - ", "")
            .Replace("Dibujo_Copia_", "")
            .Trim();

        bool isTutorialActive = WorkshopTutorialManager.Instance != null && !WorkshopTutorialManager.Instance.IsCompleted;

        // Buscar pedido activo en la Tableta
        OrderItem targetOrder = FindMatchingOrder(cleanTitle);

        // Si el tutorial está activo y no hubo match exacto por nombre, asociar automáticamente al pedido del tutorial
        if (isTutorialActive && targetOrder == null && TabletManager.Instance != null && TabletManager.Instance.CurrentOrders.Count > 0)
        {
            targetOrder = TabletManager.Instance.CurrentOrders[0];
        }

        if (targetOrder != null)
        {
            // En el tutorial, asegurar que las copias entregadas satisfagan el pedido para no trabar el aprendizaje
            if (isTutorialActive)
            {
                targetOrder.unitsRequested = Mathf.Max(1, copiesInEnvelope);
            }
            else if (copiesInEnvelope < targetOrder.unitsRequested)
            {
                Debug.LogWarning($"<color=yellow>[Caja de Envíos]</color> ⚠️ El pedido '{targetOrder.orderId}' requiere <b>{targetOrder.unitsRequested} copias</b>, pero el sobre solo contiene <b>{copiesInEnvelope}</b>. Introduce más copias.");
                processedObjects.Remove(envelopeObj);
                return false;
            }

            // Marcar pedido como completado
            targetOrder.unitsDelivered = targetOrder.unitsRequested;
            targetOrder.isCompleted = true;
            if (targetOrder.product != null)
            {
                targetOrder.product.totalUnitsSold += targetOrder.unitsRequested;
            }

            // 1. Actualizar y borrar de la tableta
            if (TabletManager.Instance != null)
            {
                TabletManager.Instance.CompleteOrder(targetOrder);
            }

            // 2. Recompensa económica (precio * unidades requeridas)
            int totalReward = targetOrder.product != null && targetOrder.product.price > 0
                ? targetOrder.product.price * targetOrder.unitsRequested
                : basePriceFallback * targetOrder.unitsRequested;

            if (PlayerEconomy.Instance != null)
            {
                PlayerEconomy.Instance.AgregarDinero(totalReward);
            }
            else
            {
                var econ = FindAnyObjectByType<PlayerEconomy>();
                if (econ != null) econ.AgregarDinero(totalReward);
            }

            // 3. Sonido ka-ching y efecto visual flotante de dinero ("money up")
            SoundEffectManager.Play("ka-ching");
            FloatingMoneyEffect.Spawn(GetBoxCenter() + Vector3.up * 0.45f, totalReward);
            Debug.Log($"<color=green>[Caja de Envíos]</color> 🎉 <b>¡PEDIDO {targetOrder.orderId} COMPLETADO!</b> Sobre sellado con {copiesInEnvelope} copias de '{cleanTitle}' entregado con éxito. ¡Ganaste: <b>${totalReward}</b>!");

            WorkshopTutorialManager.Instance?.NotifyDeliveryCompleted();
        }
        else
        {
            // Venta libre de mercancía en sobre si no hay un pedido activo
            int freeReward = basePriceFallback * Mathf.Max(1, copiesInEnvelope);
            if (PlayerEconomy.Instance != null)
            {
                PlayerEconomy.Instance.AgregarDinero(freeReward);
            }
            else
            {
                var econ = FindAnyObjectByType<PlayerEconomy>();
                if (econ != null) econ.AgregarDinero(freeReward);
            }

            SoundEffectManager.Play("ka-ching");
            FloatingMoneyEffect.Spawn(GetBoxCenter() + Vector3.up * 0.45f, freeReward);
            Debug.Log($"<color=green>[Caja de Envíos]</color> ✉️ Sobre con {copiesInEnvelope} copia(s) vendido libremente. +${freeReward}.");

            WorkshopTutorialManager.Instance?.NotifyDeliveryCompleted();
        }

        // Liberar referencias de agarre
        if (FirstPersonPlayerController.Instance != null)
        {
            FirstPersonPlayerController.Instance.ReleaseDeliveredObject(envelopeObj.transform);
        }

        // Desactivar y destruir el sobre entregado
        envelopeObj.SetActive(false);
        Destroy(envelopeObj);

        // Asegurar que siempre queden sobres disponibles en la Mesa de Envíos
        RespawnReplacementEnvelopeIfNeeded();

        return true;
    }

    private void RespawnReplacementEnvelopeIfNeeded()
    {
        var existingEnvelopes = FindObjectsByType<EnvelopeController>(FindObjectsInactive.Exclude);
        if (existingEnvelopes.Length < 3)
        {
            if (EnvelopeDispenser.Instance != null)
            {
                EnvelopeDispenser.Instance.DispenseNewEnvelope();
            }
            else
            {
                GameObject template = GameObject.Find("Sobre") ?? GameObject.Find("Sobre (1)") ?? GameObject.Find("Sobre (2)");
                if (template != null)
                {
                    Vector3 spawnPos = new Vector3(3.30f, -0.065f, -2.55f);
                    Quaternion spawnRot = Quaternion.Euler(0f, 180f, 90f);
                    GameObject newEnv = Instantiate(template, spawnPos, spawnRot);
                    newEnv.name = $"Sobre ({UnityEngine.Random.Range(10, 99)})";
                    newEnv.SetActive(true);
                    var ctrl = newEnv.GetComponent<EnvelopeController>();
                    if (ctrl == null) ctrl = newEnv.AddComponent<EnvelopeController>();
                    ctrl.SetupPhysicsAndGrab();
                    ctrl.SetupWorldStatusUI();
                    ctrl.UpdateVisuals();
                }
            }
        }
    }

    private OrderItem FindMatchingOrder(string cleanTitle)
    {
        if (TabletManager.Instance == null) return null;
        IReadOnlyList<OrderItem> orders = TabletManager.Instance.CurrentOrders;
        if (orders == null || orders.Count == 0) return null;

        // 1. Coincidencia de título con el producto
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

        // 2. Si no coincide el título exacto, asignar al pedido pendiente más antiguo
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

public class FloatingWarningFade : MonoBehaviour
{
    private TextMesh textMesh;
    private float duration = 2.4f;

    private void Awake()
    {
        textMesh = GetComponent<TextMesh>();
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }

    private IEnumerator Start()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));

            if (t > 0.60f && textMesh != null)
            {
                Color c = textMesh.color;
                c.a = Mathf.Lerp(1f, 0f, (t - 0.60f) / 0.40f);
                textMesh.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
