using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArtSimulator
{
    /// <summary>
    /// Administrador de lápices de colores 3D en el taller.
    /// Configura y activa la paleta completa de lápices en la bandeja de trabajo (caja_lapices),
    /// asignando a cada lápiz su física ergonómica, color representativo, soporte VR y auto-rescate a la mesa.
    /// </summary>
    public class ColoredPenManager : MonoBehaviour
    {
        public static ColoredPenManager Instance { get; private set; }

        [System.Serializable]
        public struct PenColorConfig
        {
            public string colorName;
            public Color color;
            public string cylinderMeshName;
            public string textureFileName;

            public PenColorConfig(string name, Color c, string cylMesh, string texName)
            {
                colorName = name;
                color = c;
                cylinderMeshName = cylMesh;
                textureFileName = texName;
            }
        }

        private static readonly PenColorConfig[] PenPalette = new PenColorConfig[]
        {
            new PenColorConfig("Morado", new Color(0.56f, 0.15f, 0.72f, 1f), "Cylinder.003", "lapiz morado.png"),
            new PenColorConfig("Cyan", new Color(0.05f, 0.75f, 0.88f, 1f), "Cylinder.004", "lapiz cyan.png"),
            new PenColorConfig("Azul", new Color(0.12f, 0.50f, 0.90f, 1f), "Cylinder.006", "lapiz azul.png"),
            new PenColorConfig("Verde", new Color(0.20f, 0.68f, 0.25f, 1f), "Cylinder.008", "lapiz verde.png"),
            new PenColorConfig("Amarillo", new Color(0.98f, 0.86f, 0.15f, 1f), "Cylinder.010", "lapiz amarillo.png"),
            new PenColorConfig("Naranjo", new Color(0.96f, 0.52f, 0.05f, 1f), "Cylinder.012", "lapiz naranjo.png"),
            new PenColorConfig("Rojo", new Color(0.90f, 0.16f, 0.16f, 1f), "Cylinder.014", "lapiz rojo.png"),
            new PenColorConfig("Rosa", new Color(0.95f, 0.35f, 0.58f, 1f), "Cylinder.016", "lapiz rosa.png"),
            new PenColorConfig("Cafe", new Color(0.48f, 0.30f, 0.22f, 1f), "Cylinder.018", "lapiz cafe.png"),
            new PenColorConfig("Negro", new Color(0.12f, 0.12f, 0.12f, 1f), "Cylinder.020", "lapiz negro.png"),
            new PenColorConfig("Blanco", new Color(0.96f, 0.96f, 0.96f, 1f), "Cylinder.022", "lapiz blanco.png")
        };

        private readonly List<GameObject> activePencils = new List<GameObject>();
        private readonly Dictionary<string, Vector3> initialPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Quaternion> initialRotations = new Dictionary<string, Quaternion>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            RemoveLegacyPencil();
            InitializePencilsPalette();
        }

        /// <summary>
        /// Elimina cualquier lápiz cilíndrico provisional de versiones previas
        /// </summary>
        public void RemoveLegacyPencil()
        {
            var oldPencils = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < oldPencils.Length; i++)
            {
                if (oldPencils[i] != null && oldPencils[i].name.Equals("VRPen_Stylus", StringComparison.OrdinalIgnoreCase))
                {
                    Destroy(oldPencils[i]);
                }
            }
        }

        /// <summary>
        /// Inicializa los 11 lápices de colores.
        /// Prioriza la bandeja 3D modelada 'caja_lapices', o genera la paleta sobre la mesa.
        /// </summary>
        public void InitializePencilsPalette()
        {
            activePencils.Clear();
            initialPositions.Clear();
            initialRotations.Clear();

            GameObject cajaObj = GameObject.Find("caja_lapices");
            if (cajaObj != null)
            {
                SetupBoxAndPencils(cajaObj);
            }
            else
            {
                SpawnProceduralPencils();
            }

            Debug.Log($"<color=green>[ColoredPenManager]</color> Colección de {activePencils.Count} lápices de colores configurada y lista sobre la mesa.");
        }

        private void SetupBoxAndPencils(GameObject cajaObj)
        {
            // 1. Asegurar collider sólido en la caja/bandeja (Cube) para reposar firmemente en la mesa
            Transform cube = cajaObj.transform.Find("Cube");
            if (cube != null)
            {
                var bc = cube.GetComponent<BoxCollider>();
                if (bc == null) bc = cube.gameObject.AddComponent<BoxCollider>();
            }

            // Contenedor a escala 1 para albergar los lápices sin deformaciones de escalado de padres
            GameObject holder = GameObject.Find("PencilsHolder");
            if (holder == null)
            {
                holder = new GameObject("PencilsHolder");
                holder.transform.position = cajaObj.transform.position;
                holder.transform.rotation = Quaternion.identity;
                holder.transform.localScale = Vector3.one;
            }

            for (int i = 0; i < PenPalette.Length; i++)
            {
                var cfg = PenPalette[i];
                Transform penTransform = cajaObj.transform.Find(cfg.cylinderMeshName);
                if (penTransform == null)
                {
                    penTransform = cajaObj.transform.Find($"Lapiz_{cfg.colorName}");
                }
                if (penTransform == null)
                {
                    penTransform = holder.transform.Find($"Lapiz_{cfg.colorName}");
                }
                if (penTransform == null) continue;

                // Desvincular de la escala reducida de caja_lapices preservando la posición y orientación en la bandeja
                penTransform.SetParent(holder.transform, true);
                penTransform.name = $"Lapiz_{cfg.colorName}";

                // 2. Colisionador físico adaptado a la orientación Z del lápiz
                var capsule = penTransform.GetComponent<CapsuleCollider>();
                if (capsule == null) capsule = penTransform.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = 2; // Eje Z
                capsule.radius = 0.15f;
                capsule.height = 2.2f;
                capsule.center = Vector3.zero;

                // 3. Rigidbody cinemático en reposo: estabilidad absoluta sin caerse ni vibrar
                var rb = penTransform.GetComponent<Rigidbody>();
                if (rb == null) rb = penTransform.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.04f;
                rb.isKinematic = true; // Estable mientras esté en reposo
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // 4. Identificar la punta (Cone.00x) y afinar su visual y trigger
                Transform coneTransform = penTransform.childCount > 0 ? penTransform.GetChild(0) : penTransform;
                if (coneTransform != null && coneTransform != penTransform)
                {
                    var tipRend = coneTransform.GetComponent<Renderer>();
                    if (tipRend != null)
                    {
                        Material tipMat = Application.isPlaying ? tipRend.material : tipRend.sharedMaterial;
                        if (tipMat != null)
                        {
                            if (tipMat.HasProperty("_BaseColor")) tipMat.SetColor("_BaseColor", cfg.color);
                            if (tipMat.HasProperty("_Color")) tipMat.SetColor("_Color", cfg.color);
                        }
                    }

                    var tipTrigger = coneTransform.GetComponent<SphereCollider>();
                    if (tipTrigger == null) tipTrigger = coneTransform.gameObject.AddComponent<SphereCollider>();
                    tipTrigger.isTrigger = true;
                    tipTrigger.radius = 0.15f;
                }

                // 5. Punto de agarre ergonómico para VR (la punta apunta hacia adelante del mando)
                Transform attachPoint = penTransform.Find("AttachPoint");
                if (attachPoint == null)
                {
                    GameObject ap = new GameObject("AttachPoint");
                    ap.transform.SetParent(penTransform, false);
                    attachPoint = ap.transform;
                }
                attachPoint.localPosition = new Vector3(0f, 0f, -0.4f);
                attachPoint.localRotation = Quaternion.Euler(-45f, 180f, 0f);

                // 6. XRGrabInteractable
                var grab = penTransform.GetComponent<XRGrabInteractable>();
                if (grab == null) grab = penTransform.gameObject.AddComponent<XRGrabInteractable>();
                grab.attachTransform = attachPoint;
                grab.throwOnDetach = true;
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;

                grab.selectEntered.RemoveListener(OnPencilGrabbed);
                grab.selectEntered.AddListener(OnPencilGrabbed);
                grab.selectExited.RemoveListener(OnPencilReleased);
                grab.selectExited.AddListener(OnPencilReleased);

                // 7. Componente de dibujo con su color y punta exacta
                var penTip = penTransform.GetComponent<VRPenTip>();
                if (penTip == null) penTip = penTransform.gameObject.AddComponent<VRPenTip>();
                penTip.SetTipTransform(coneTransform);
                penTip.SetColor(cfg.color);
                penTip.SetBrushSize(8);

                activePencils.Add(penTransform.gameObject);
                initialPositions[penTransform.name] = penTransform.position;
                initialRotations[penTransform.name] = penTransform.rotation;
            }
        }

        private void SpawnProceduralPencils()
        {
            GameObject mesaObj = GameObject.Find("Mesa1.0");
            Vector3 mesaPos = mesaObj != null ? mesaObj.transform.position : new Vector3(5.62f, -1.45f, -0.15f);
            float tableY = -0.065f;
            float startX = 5.65f;
            float stepX = 0.085f;

            for (int i = 0; i < PenPalette.Length; i++)
            {
                var cfg = PenPalette[i];
                string pencilId = $"Lapiz_{cfg.colorName}";

                GameObject existing = GameObject.Find(pencilId);
                if (existing != null)
                {
                    activePencils.Add(existing);
                    initialPositions[pencilId] = existing.transform.position;
                    initialRotations[pencilId] = existing.transform.rotation;
                    continue;
                }

                int row = i < 6 ? 0 : 1;
                int col = i < 6 ? i : (i - 6);

                float posX = startX + (col * stepX);
                float posZ = row == 0 ? 0.04f : -0.06f;
                Vector3 spawnPos = new Vector3(posX, tableY, posZ);

                GameObject penRoot = CreateProceduralPencil(cfg, spawnPos);
                activePencils.Add(penRoot);
                initialPositions[pencilId] = spawnPos;
                initialRotations[pencilId] = penRoot.transform.rotation;
            }
        }

        private GameObject CreateProceduralPencil(PenColorConfig cfg, Vector3 position)
        {
            GameObject penRoot = new GameObject($"Lapiz_{cfg.colorName}");
            penRoot.transform.position = position;
            penRoot.transform.rotation = Quaternion.Euler(0f, 75f, 90f);

            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            Texture2D penTex = null;
#if UNITY_EDITOR
            penTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/modelos y texturas/texturas/{cfg.textureFileName}");
#endif

            Material bodyMat = new Material(uLit);
            bodyMat.name = $"M_Lapiz_{cfg.colorName}";
            bodyMat.color = cfg.color;
            if (penTex != null) bodyMat.mainTexture = penTex;
            bodyMat.SetFloat("_Smoothness", 0.4f);

            Material tipMat = new Material(uLit);
            tipMat.name = $"M_Punta_{cfg.colorName}";
            tipMat.color = cfg.color;

            GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bodyObj.name = "Body";
            bodyObj.transform.SetParent(penRoot.transform, false);
            bodyObj.transform.localPosition = Vector3.zero;
            bodyObj.transform.localScale = new Vector3(0.016f, 0.080f, 0.016f);
            Collider origCol = bodyObj.GetComponent<Collider>();
            if (origCol != null) Destroy(origCol);
            var bodyRenderer = bodyObj.GetComponent<Renderer>();
            if (bodyRenderer != null) bodyRenderer.sharedMaterial = bodyMat;

            GameObject tipObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tipObj.name = "Tip";
            tipObj.transform.SetParent(penRoot.transform, false);
            tipObj.transform.localPosition = new Vector3(0f, 0.088f, 0f);
            tipObj.transform.localScale = new Vector3(0.007f, 0.012f, 0.007f);
            Collider tipCol = tipObj.GetComponent<Collider>();
            if (tipCol != null) Destroy(tipCol);
            var tipRenderer = tipObj.GetComponent<Renderer>();
            if (tipRenderer != null) tipRenderer.sharedMaterial = tipMat;

            CapsuleCollider capsule = penRoot.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.014f;
            capsule.height = 0.19f;
            capsule.center = new Vector3(0f, 0.01f, 0f);

            Rigidbody rb = penRoot.AddComponent<Rigidbody>();
            rb.mass = 0.04f;
            rb.isKinematic = true; // Cinemático al inicio para que no caiga
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            GameObject attachObj = new GameObject("AttachPoint");
            attachObj.transform.SetParent(penRoot.transform, false);
            attachObj.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            attachObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var grab = penRoot.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attachObj.transform;
            grab.throwOnDetach = true;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;

            grab.selectEntered.AddListener(OnPencilGrabbed);
            grab.selectExited.AddListener(OnPencilReleased);

            VRPenTip penTip = penRoot.AddComponent<VRPenTip>();
            penTip.SetColor(cfg.color);
            penTip.SetBrushSize(8);

            return penRoot;
        }

        private void OnPencilGrabbed(SelectEnterEventArgs args)
        {
            if (args != null && args.interactableObject != null)
            {
                var rb = args.interactableObject.transform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
            }
        }

        private void OnPencilReleased(SelectExitEventArgs args)
        {
            if (args != null && args.interactableObject != null)
            {
                var rb = args.interactableObject.transform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
            }
        }

        private void Update()
        {
            // Auto-rescate: Si algún lápiz cae debajo de la mesa (Y < -0.35m) y no está siendo sostenido,
            // devolverlo suavemente a su ranura en la mesa o bandeja.
            for (int i = 0; i < activePencils.Count; i++)
            {
                GameObject p = activePencils[i];
                if (p == null) continue;

                var grab = p.GetComponent<XRGrabInteractable>();
                if (grab != null && grab.isSelected) continue;

                if (p.transform.position.y < -0.35f)
                {
                    if (initialPositions.TryGetValue(p.name, out Vector3 initPos))
                    {
                        var rb = p.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.linearVelocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                            rb.isKinematic = true;
                        }
                        p.transform.position = initPos;
                        if (initialRotations.TryGetValue(p.name, out Quaternion initRot))
                        {
                            p.transform.rotation = initRot;
                        }
                        Debug.Log($"<color=cyan>[ColoredPenManager]</color> Lápiz <b>{p.name}</b> rescatado del suelo y retornado a su posición.");
                    }
                }
            }
        }
    }
}
