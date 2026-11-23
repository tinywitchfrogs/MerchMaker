using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ArtSimulator
{
    /// <summary>
    /// Controlador del Sobre de Envíos.
    /// Permite introducir copias impresas de una ilustración hasta completar las requeridas por un pedido.
    /// Al cerrarse/sellarse, el sobre puede depositarse en la caja de envíos para completar la venta.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EnvelopeController : MonoBehaviour
    {
        [Header("Estado del Sobre")]
        [SerializeField] private int currentCopiesCount = 0;
        [SerializeField] private string artworkTitle = "";
        [SerializeField] private string customerName = "";
        [SerializeField] private bool isSealed = false;
        [SerializeField] private int targetOrderCopies = 1;

        public int CurrentCopiesCount => currentCopiesCount;
        public string ArtworkTitle => artworkTitle;
        public string CustomerName => customerName;
        public bool IsSealed => isSealed;
        public int TargetOrderCopies => targetOrderCopies;

        [Header("Punto de Inserción")]
        [SerializeField] private Transform slotTransform;

        private AudioSource audioSource;
        private readonly List<GameObject> containedCopies = new List<GameObject>();
        private Text statusText;
        private Button sealButton;
        private XRGrabInteractable grabInteractable;
        private GameObject envelopeCanvasObj;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }

            SetupPhysicsAndGrab();
            SetupWorldStatusUI();
            if (isSealed) FoldFlapMesh();
            UpdateVisuals();
        }

        private void Update()
        {
            CheckBoundsAndRespawn();
            CheckNearbyCopies();
        }

        private void CheckNearbyCopies()
        {
            if (isSealed || (targetOrderCopies > 0 && currentCopiesCount >= targetOrderCopies)) return;

            var drawings = FindObjectsByType<ScannableDrawing>(FindObjectsInactive.Exclude);
            for (int i = 0; i < drawings.Length; i++)
            {
                var d = drawings[i];
                if (d == null || d.IsOriginal || !d.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(transform.position, d.transform.position);
                if (dist <= 0.38f)
                {
                    TryInsertCopy(d);
                    break;
                }
            }
        }

        public void CheckBoundsAndRespawn()
        {
            // Solo rescatar en caso extremo si el sobre cae al vacío infinito atravesando el suelo (Y < -5.0f)
            // No forzar ninguna posición fija: si cae al suelo, el jugador puede recogerlo libremente donde haya caído.
            if (transform.position.y < -5.0f)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = false;
                }
                transform.position = new Vector3(3.30f, -1.20f, -2.85f); // En el suelo de la oficina bajo la mesa
            }
        }

        public void RespawnOnDesk()
        {
            // Método mantenido por compatibilidad si es invocado externamente
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        public void SetupPhysicsAndGrab()
        {
            // Eliminar luces o cámaras heredadas de la importación de Blender (ej. Point Light de 1000W)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform ch = transform.GetChild(i);
                if (ch.GetComponent<Light>() != null || ch.GetComponent<Camera>() != null || ch.name.ToLower().Contains("light") || ch.name.ToLower().Contains("camera"))
                {
                    if (Application.isPlaying) Destroy(ch.gameObject);
                    else DestroyImmediate(ch.gameObject);
                }
            }

            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.linearDamping = 2.0f;
            rb.angularDamping = 3.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Asegurar que Cube esté en su posición esperada
            Transform cube = transform.Find("Cube");
            if (cube != null)
            {
                cube.localPosition = new Vector3(0f, 1.68f, 0f);
            }

            // BoxCollider físico ajustado con holgura ergonómica para facilitar el agarre tanto con mano directa como rayo láser en VR y PC
            var bc = GetComponent<BoxCollider>();
            if (bc == null) bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0.00f, 1.68f, 0f);
            bc.size = new Vector3(1.20f, 2.40f, 2.80f);

            // Trigger secundario para absorber copias
            var triggerObj = transform.Find("InsertTrigger");
            if (triggerObj == null)
            {
                GameObject tObj = new GameObject("InsertTrigger");
                tObj.transform.SetParent(transform, false);
                tObj.transform.localPosition = new Vector3(0.025f, 1.68f, 0f);
                var tCol = tObj.AddComponent<BoxCollider>();
                tCol.isTrigger = true;
                tCol.size = new Vector3(0.60f, 2.00f, 1.80f);
            }

            grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
            {
                grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
            }
            // Agarre instantáneo y suave: elimina por completo el efecto elástico y bamboleo ("yendo y viniendo")
            grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grabInteractable.throwOnDetach = false;

            grabInteractable.selectEntered.RemoveListener(OnEnvelopeGrabbed);
            grabInteractable.selectEntered.AddListener(OnEnvelopeGrabbed);

            grabInteractable.selectExited.RemoveListener(OnEnvelopeReleased);
            grabInteractable.selectExited.AddListener(OnEnvelopeReleased);

            // En VR: al sostener el sobre y apretar el gatillo (Trigger), sellar el sobre directamente
            grabInteractable.activated.RemoveListener(OnVRTriggerActivated);
            grabInteractable.activated.AddListener(OnVRTriggerActivated);
        }

        private void OnEnvelopeGrabbed(SelectEnterEventArgs args)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }
        }

        private void OnEnvelopeReleased(SelectExitEventArgs args)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, 1.2f);
                rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 2.5f);
            }
        }

        private void OnVRTriggerActivated(ActivateEventArgs args)
        {
            if (!isSealed && currentCopiesCount > 0)
            {
                SealEnvelope();
            }
        }

        public void SetupWorldStatusUI()
        {
            Transform existingCanvas = transform.Find("EnvelopeCanvas");
            if (existingCanvas != null)
            {
                // 0.055f en local X equivale a 4.5 mm de holgura por encima de la cara del sobre, evitando Z-fighting
                existingCanvas.localPosition = new Vector3(0.055f, 1.68f, 0f);
                existingCanvas.localRotation = Quaternion.Euler(0f, 90f, 90f);
                existingCanvas.localScale = new Vector3(-0.0025f, 0.0025f, 0.0025f);

                Canvas existingC = existingCanvas.GetComponent<Canvas>();
                if (existingC != null)
                {
                    existingC.overrideSorting = true;
                    existingC.sortingOrder = 5;
                }

                // Asegurar raycasters para que los botones respondan a clics y rayos VR
                if (existingCanvas.GetComponent<GraphicRaycaster>() == null)
                    existingCanvas.gameObject.AddComponent<GraphicRaycaster>();
                if (existingCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                    existingCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

                statusText = existingCanvas.GetComponentInChildren<Text>();
                sealButton = existingCanvas.GetComponentInChildren<Button>();
                envelopeCanvasObj = existingCanvas.gameObject;
                return;
            }

            GameObject canvasObj = new GameObject("EnvelopeCanvas");
            canvasObj.transform.SetParent(transform, false);
            envelopeCanvasObj = canvasObj;
            // 0.055f para reposar firmemente pegado encima de la cara del sobre
            canvasObj.transform.localPosition = new Vector3(0.055f, 1.68f, 0f);
            canvasObj.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
            canvasObj.transform.localScale = new Vector3(-0.0025f, 0.0025f, 0.0025f);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5;
            canvasObj.AddComponent<GraphicRaycaster>();
            canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvasObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360, 240);

            // Fondo sutil de la etiqueta
            GameObject labelBg = new GameObject("LabelBg");
            labelBg.transform.SetParent(canvasObj.transform, false);
            var lbgRt = labelBg.AddComponent<RectTransform>();
            lbgRt.anchorMin = Vector2.zero;
            lbgRt.anchorMax = Vector2.one;
            lbgRt.offsetMin = Vector2.zero;
            lbgRt.offsetMax = Vector2.zero;
            Image bgImg = labelBg.AddComponent<Image>();
            bgImg.color = new Color(0.96f, 0.94f, 0.88f, 0.95f); // Papel kraft claro
            bgImg.raycastTarget = false;

            // Texto de estado
            GameObject textObj = new GameObject("StatusText");
            textObj.transform.SetParent(canvasObj.transform, false);
            var tRt = textObj.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.05f, 0.40f);
            tRt.anchorMax = new Vector2(0.95f, 0.95f);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            statusText = textObj.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 22;
            statusText.fontStyle = FontStyle.Bold;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = new Color(0.2f, 0.15f, 0.1f);
            statusText.text = "✉️ SOBRE VACÍO\nInserta las copias aquí";
            statusText.raycastTarget = false;

            // Botón interactivo para Cerrar/Sellar Sobre
            GameObject btnObj = new GameObject("SealButton");
            btnObj.transform.SetParent(canvasObj.transform, false);
            var bRt = btnObj.AddComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.08f, 0.08f);
            bRt.anchorMax = new Vector2(0.92f, 0.38f);
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.65f, 0.30f); // Verde sellado vibrante
            sealButton = btnObj.AddComponent<Button>();
            sealButton.targetGraphic = btnImg;
            sealButton.onClick.AddListener(SealEnvelope);

            var trig = btnObj.AddComponent<TabletButtonTrigger>();
            trig.SetAction("Cerrar Sobre", SealEnvelope);

            GameObject btnTxtObj = new GameObject("BtnText");
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            var btRt = btnTxtObj.AddComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;
            Text btnTxt = btnTxtObj.AddComponent<Text>();
            btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnTxt.fontSize = 20;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.color = Color.white;
            btnTxt.text = "🔒 CERRAR SOBRE";
            btnTxt.raycastTarget = false;
        }

        public bool TryInsertCopy(ScannableDrawing copy)
        {
            if (copy == null || isSealed) return false;

            // Si es original, advertir al jugador (no se envían los originales)
            if (copy.IsOriginal)
            {
                Debug.LogWarning("<color=yellow>[Sobre]</color> No puedes meter la ilustración original en el sobre. ¡Solo debes introducir las copias impresas!");
                return false;
            }

            string copyTitle = copy.GetArtworkData().title
                .Replace("Copia - ", "")
                .Replace("Dibujo_Copia_", "")
                .Trim();

            // 1. Determinar targetOrderCopies antes de insertar si aún no está fijado
            if (targetOrderCopies <= 0 || string.IsNullOrEmpty(customerName))
            {
                if (TabletManager.Instance != null && TabletManager.Instance.CurrentOrders != null)
                {
                    for (int i = 0; i < TabletManager.Instance.CurrentOrders.Count; i++)
                    {
                        var ord = TabletManager.Instance.CurrentOrders[i];
                        if (ord != null && !ord.isCompleted && ord.product != null)
                        {
                            if (ord.product.title.Equals(copyTitle, System.StringComparison.OrdinalIgnoreCase))
                            {
                                customerName = ord.GetCustomerName();
                                targetOrderCopies = ord.unitsRequested;
                                break;
                            }
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(customerName)) customerName = "Cliente";
            if (targetOrderCopies <= 0) targetOrderCopies = 1;

            // 2. REQUISITO ESTRICTO: Si el sobre ya tiene todas las copias dentro, NO PERMITIR colocar más
            if (currentCopiesCount >= targetOrderCopies)
            {
                Debug.LogWarning($"<color=yellow>[Sobre]</color> ⚠️ El sobre ya tiene todas sus copias ({currentCopiesCount}/{targetOrderCopies}). ¡No se permiten más copias!");
                ShowCapacityFullFeedback();
                if (!isSealed) SealEnvelope();
                return false;
            }

            // Si el sobre ya tiene copias, verificar que sea del mismo arte
            if (currentCopiesCount > 0 && !string.IsNullOrEmpty(artworkTitle))
            {
                if (!copyTitle.Equals(artworkTitle, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"<color=yellow>[Sobre]</color> Este sobre es para '{artworkTitle}'. No puedes mezclar copias de '{copyTitle}' en el mismo sobre.");
                    return false;
                }
            }
            else
            {
                artworkTitle = copyTitle;
            }

            // Liberar de la mano del jugador si lo estaba sosteniendo
            if (FirstPersonPlayerController.Instance != null && FirstPersonPlayerController.Instance.IsHoldingObject(copy.transform))
            {
                FirstPersonPlayerController.Instance.ReleaseDeliveredObject(copy.transform);
            }

            var grab = copy.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected && grab.interactionManager != null)
            {
                var interactor = grab.firstInteractorSelecting;
                if (interactor != null)
                {
                    grab.interactionManager.SelectExit(interactor, grab);
                }
            }

            // Guardar e introducir copia
            currentCopiesCount++;
            copy.gameObject.SetActive(false);
            containedCopies.Add(copy.gameObject);

            PlayPaperSlideSound();

            // Cierre automático: apenas se introducen las copias requeridas, el sobre se cierra y sella solo
            if (currentCopiesCount >= targetOrderCopies)
            {
                SealEnvelope();
            }
            else
            {
                UpdateVisuals();
            }

            Debug.Log($"<color=green>[Sobre]</color> Copia física #{currentCopiesCount}/{targetOrderCopies} de <b>'{artworkTitle}'</b> introducida en el sobre para {customerName}.");
            return true;
        }

        private void ShowCapacityFullFeedback()
        {
            if (statusText != null)
            {
                statusText.text = $"🔒 SOBRE COMPLETO\n({currentCopiesCount}/{targetOrderCopies} copias listadas)";
                statusText.color = new Color(0.85f, 0.40f, 0.10f); // Naranja aviso
            }
        }

        public void FoldFlapMesh()
        {
            var mf = GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;

            try
            {
                Mesh m = Instantiate(mf.sharedMesh);
                Vector3[] verts = m.vertices;
                bool modified = false;

                for (int i = 0; i < verts.Length; i++)
                {
                    if (verts[i].z > 1.001f)
                    {
                        float dist = verts[i].z - 1.0f;
                        // Doblar la solapa hacia abajo contra el cuerpo del sobre
                        verts[i] = new Vector3(-verts[i].x * 0.95f, verts[i].y, 1.0f - dist * 0.95f);
                        modified = true;
                    }
                }

                if (modified)
                {
                    m.vertices = verts;
                    m.RecalculateNormals();
                    m.RecalculateBounds();
                    mf.mesh = m;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Sobre] No se pudo deformar solapa: {ex.Message}");
            }
        }

        public void SealEnvelope()
        {
            if (isSealed) return;

            if (currentCopiesCount <= 0)
            {
                Debug.LogWarning("<color=yellow>[Sobre]</color> No puedes cerrar un sobre vacío. Introduce primero las copias impresas.");
                return;
            }

            isSealed = true;
            FoldFlapMesh();
            PlaySealSound();
            UpdateVisuals();

            Debug.Log($"<color=green>[Sobre]</color> 🔒 ¡Sobre cerrado y sellado automáticamente! Destinatario: {customerName} | Contiene <b>{currentCopiesCount} copias</b> de '{artworkTitle}'.");

            // Notificar al tutorial
            WorkshopTutorialManager.Instance?.NotifyEnvelopeSealed(this);
        }

        public void UpdateVisuals()
        {
            if (statusText == null) SetupWorldStatusUI();

            bool showLabel = isSealed || currentCopiesCount > 0;
            if (envelopeCanvasObj != null)
            {
                envelopeCanvasObj.SetActive(showLabel);
            }

            if (statusText != null && showLabel)
            {
                if (isSealed)
                {
                    statusText.text = $"✉️ <b>DESTINATARIO:</b> {customerName}\n" +
                                      $"<b>Remitente:</b> Suny Merch\n" +
                                      $"<b>Contenido:</b> {currentCopiesCount} {(currentCopiesCount == 1 ? "copia" : "copias")} de '{artworkTitle}'\n" +
                                      $"<color=#15803d>✓ LISTO PARA ENVIAR</color>";
                }
                else
                {
                    statusText.text = $"✉️ <b>DESTINATARIO:</b> {customerName}\n" +
                                      $"<b>Progreso:</b> {currentCopiesCount} / {targetOrderCopies} copias\n" +
                                      $"<color=orange>Introduce {targetOrderCopies - currentCopiesCount} más</color>";
                }
            }

            if (sealButton != null)
            {
                sealButton.gameObject.SetActive(!isSealed && currentCopiesCount > 0);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isSealed || (targetOrderCopies > 0 && currentCopiesCount >= targetOrderCopies)) return;
            var drawing = other.GetComponent<ScannableDrawing>() ?? other.GetComponentInParent<ScannableDrawing>();
            if (drawing != null && !drawing.IsOriginal)
            {
                TryInsertCopy(drawing);
            }
        }

        private void PlayPaperSlideSound()
        {
            if (audioSource == null) return;
            int sampleRate = 44100;
            float duration = 0.12f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.PI * (float)i / sampleCount);
                // Ruido blanco filtrado simulando roce de papel
                samples[i] = (Random.value * 2f - 1f) * env * 0.18f;
            }

            AudioClip clip = AudioClip.Create("PaperSlide", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }

        private void PlaySealSound()
        {
            if (audioSource == null) return;
            int sampleRate = 44100;
            float duration = 0.15f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1.0f - ((float)i / sampleCount);
                samples[i] = Mathf.Sin(2 * Mathf.PI * 650 * t) * env * 0.25f;
            }

            AudioClip clip = AudioClip.Create("SealSound", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < containedCopies.Count; i++)
            {
                if (containedCopies[i] != null)
                {
                    Destroy(containedCopies[i]);
                }
            }
            containedCopies.Clear();
        }
    }
}
