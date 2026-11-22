using UnityEngine;
using UnityEngine.EventSystems;

namespace ArtSimulator
{
    public static class RuntimeSimulatorInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeScene()
        {
            // 0. Colisionadores de la Oficina (Paredes, suelo, puerta y techo para no atravesar paredes)
            GameObject oficinaObj = GameObject.Find("oficina");
            if (oficinaObj != null)
            {
                foreach (Transform t in oficinaObj.transform)
                {
                    var mf = t.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = t.GetComponent<MeshCollider>();
                        if (mc == null) mc = t.gameObject.AddComponent<MeshCollider>();
                    }
                }
            }

            // 1. Tableta
            GameObject tabletObj = GameObject.Find("Tableta");
            if (tabletObj != null)
            {
                if (tabletObj.GetComponent<TabletManager>() == null)
                    tabletObj.AddComponent<TabletManager>();

                TabletUIBuilder uiBuilder = tabletObj.GetComponent<TabletUIBuilder>();
                if (uiBuilder == null)
                    uiBuilder = tabletObj.AddComponent<TabletUIBuilder>();

                if (tabletObj.GetComponent<TabletUIController>() == null)
                    tabletObj.AddComponent<TabletUIController>();

                uiBuilder.BuildUIIfNeeded();

                TabletInteractiveScreen screenInt = tabletObj.GetComponent<TabletInteractiveScreen>();
                if (screenInt == null) screenInt = tabletObj.AddComponent<TabletInteractiveScreen>();
                screenInt.EnsureCollidersAndVisuals();

                Rigidbody rb = tabletObj.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                // Asegurar que la pantalla de la tableta empiece oculta
                TabletManager tm = tabletObj.GetComponent<TabletManager>();
                if (tm != null)
                {
                    tm.CloseTablet();
                }
            }

            // 2. Impresora
            GameObject printerObj = GameObject.Find("impresora color (1)") ?? GameObject.Find("Impresora");
            if (printerObj != null)
            {
                var pc = printerObj.GetComponent<PrinterController>() ?? printerObj.GetComponentInParent<PrinterController>();
                if (pc == null) pc = printerObj.AddComponent<PrinterController>();
            }

            // 3. Dibujos Interactivos (Originales y lienzos para pintar)
            GameObject baseDrawing = GameObject.Find("Dibujo") ?? GameObject.Find("Dibujo_1");
            if (baseDrawing != null)
            {
                baseDrawing.name = "Dibujo_1";
            }

            System.Collections.Generic.List<GameObject> drawingObjects = new System.Collections.Generic.List<GameObject>();
            foreach (var go in Object.FindObjectsByType<GameObject>())
            {
                if (go.name.StartsWith("Dibujo") && !go.name.Contains("Copia"))
                {
                    drawingObjects.Add(go);
                }
            }

            for (int i = 0; i < drawingObjects.Count; i++)
            {
                var drawingObj = drawingObjects[i];
                var scannable = drawingObj.GetComponent<ScannableDrawing>();
                if (scannable == null) scannable = drawingObj.AddComponent<ScannableDrawing>();

                if (drawingObj.GetComponent<Collider>() == null)
                {
                    var bc = drawingObj.AddComponent<BoxCollider>();
                    bc.size = new Vector3(10f, 0.5f, 10f);
                }

                if (drawingObj.GetComponent<DrawingCanvas>() == null)
                    drawingObj.AddComponent<DrawingCanvas>();
            }

            // 4. Inicializar interfaz de dibujo DrawingUIOverlay
            var drawingOverlay = DrawingUIOverlay.Instance;


            // 5. EventSystem
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }

            // 6. Controlador de Jugador FirstPersonPlayerController (solo PC/Editor)
            if (Application.platform != RuntimePlatform.Android)
            {
                Camera cam = Camera.main;
                if (cam != null && cam.GetComponent<FirstPersonPlayerController>() == null)
                {
                    cam.gameObject.AddComponent<FirstPersonPlayerController>();
                }
            }

            // 7. Caja de Envíos (SendProduct)
            GameObject boxObj = GameObject.Find("CajaEnvios") ?? GameObject.Find("Caja");
            if (boxObj != null)
            {
                var existingSend = boxObj.GetComponentInChildren<SendProduct>();
                if (existingSend == null)
                {
                    boxObj.AddComponent<SendProduct>();
                    var col = boxObj.GetComponent<Collider>();
                    if (col == null)
                    {
                        var bc = boxObj.AddComponent<BoxCollider>();
                        bc.isTrigger = true;
                        bc.size = new Vector3(0.5f, 0.5f, 0.5f);
                    }
                    else
                    {
                        col.isTrigger = true;
                    }
                }
            }

            // 8. Control de tienda manual: No se auto-publican productos ni pedidos.
            // El jugador tiene control total para publicar sus propias obras/fotos y fijar su precio.

            // 9. Pila de Hojas Limpias (PaperDispenser)
            SetupPaperDispenser();

            // 10. Administrador de Realidad Virtual (XRPlayerRigManager)
            if (XRPlayerRigManager.Instance == null && Object.FindAnyObjectByType<XRPlayerRigManager>() == null)
            {
                GameObject xrManagerObj = new GameObject("XRPlayerRigManager");
                xrManagerObj.AddComponent<XRPlayerRigManager>();
            }
        }

        private static void SetupPaperDispenser()
        {
            if (PaperDispenser.Instance != null || GameObject.Find("PilaDeHojas") != null) return;

            GameObject d3 = GameObject.Find("Dibujo_3") ?? GameObject.Find("Dibujo_1") ?? GameObject.Find("Dibujo");
            Vector3 dispenserPos = d3 != null 
                ? (d3.transform.position + new Vector3(-0.55f, 0f, 0f)) 
                : new Vector3(7.75f, 0.5937f, -7.1495f);

            GameObject stackObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stackObj.name = "PilaDeHojas";
            stackObj.transform.position = dispenserPos;
            stackObj.transform.rotation = d3 != null ? d3.transform.rotation : Quaternion.Euler(0, 175.08f, 0);
            stackObj.transform.localScale = new Vector3(0.42f, 0.05f, 0.32f);

            // Material para la pila de papel
            Renderer mr = stackObj.GetComponent<Renderer>();
            if (mr != null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(litShader);
                mat.color = new Color(0.96f, 0.96f, 0.95f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.96f, 0.96f, 0.95f));
                mr.material = mat;
            }

            // Bandeja base estilo organizador de escritorio
            GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "BandejaSoporte";
            tray.transform.SetParent(stackObj.transform, false);
            tray.transform.localPosition = new Vector3(0, -0.45f, 0);
            tray.transform.localScale = new Vector3(1.10f, 0.25f, 1.10f);
            var trayCol = tray.GetComponent<Collider>();
            if (trayCol != null) Object.Destroy(trayCol);

            Renderer tr = tray.GetComponent<Renderer>();
            if (tr != null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material tMat = new Material(litShader);
                tMat.color = new Color(0.20f, 0.16f, 0.12f);
                if (tMat.HasProperty("_BaseColor")) tMat.SetColor("_BaseColor", new Color(0.20f, 0.16f, 0.12f));
                tr.material = tMat;
            }

            // Ajustar colisionador para que sea fácil de mirar e interactuar
            BoxCollider bc = stackObj.GetComponent<BoxCollider>();
            if (bc != null)
            {
                bc.center = new Vector3(0f, 0.5f, 0f);
                bc.size = new Vector3(1.2f, 2.5f, 1.2f);
            }

            stackObj.AddComponent<PaperDispenser>();
            Debug.Log($"<color=green>[Simulador]</color> Pila de hojas para dibujo interactivo configurada en {dispenserPos}.");
        }
    }
}
