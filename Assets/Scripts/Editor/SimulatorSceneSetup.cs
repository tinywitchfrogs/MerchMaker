#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArtSimulator.Editor
{
    public static class SimulatorSceneSetup
    {
        [MenuItem("Simulador/Configurar Escena Completa (Tableta, Impresora, Dibujo)")]
        public static void SetupScene()
        {
            // 1. Configurar Tableta
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

                // Construir la UI
                uiBuilder.BuildUIIfNeeded();

                TabletInteractiveScreen screenInt = tabletObj.GetComponent<TabletInteractiveScreen>();
                if (screenInt == null) screenInt = tabletObj.AddComponent<TabletInteractiveScreen>();
                screenInt.EnsureCollidersAndVisuals();

                Rigidbody rb = tabletObj.GetComponent<Rigidbody>();
                if (rb != null) Object.DestroyImmediate(rb);

                Debug.Log("<color=green>[Simulador]</color> Tableta configurada con éxito como objeto estático e interactivo.");
            }
            else
            {
                Debug.LogWarning("[Simulador] No se encontró el GameObject 'Tableta' en la escena activa.");
            }

            // 2. Configurar Impresora
            GameObject printerObj = GameObject.Find("Impresora");
            if (printerObj != null)
            {
                if (printerObj.GetComponent<PrinterController>() == null)
                    printerObj.AddComponent<PrinterController>();

                // Asegurar un Collider de escaneo
                Collider col = printerObj.GetComponent<Collider>();
                if (col == null)
                {
                    BoxCollider box = printerObj.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(1.5f, 1.5f, 1.5f);
                }
                Debug.Log("<color=green>[Simulador]</color> Impresora configurada con éxito.");
            }
            else
            {
                Debug.LogWarning("[Simulador] No se encontró el GameObject 'Impresora' en la escena activa.");
            }

            // 3. Configurar Hoja de Dibujo Original (Dibujo_1)
            GameObject baseDrawing = GameObject.Find("Dibujo") ?? GameObject.Find("Dibujo_1");
            if (baseDrawing != null)
            {
                baseDrawing.name = "Dibujo_1";
                ConfigureDrawingSheet(baseDrawing, "Dibujo 1", new Vector3(9.4023f, 0.5937f, -7.1495f));

                // Eliminar posibles copias residuales antiguas en la mesa
                GameObject oldD2 = GameObject.Find("Dibujo_2");
                if (oldD2 != null) Object.DestroyImmediate(oldD2);
                GameObject oldD3 = GameObject.Find("Dibujo_3");
                if (oldD3 != null) Object.DestroyImmediate(oldD3);

                Debug.Log("<color=green>[Simulador]</color> Hoja de dibujo original configurada con éxito con soporte para pintura interactiva.");

                // Configurar dispensador de hojas de papel limpias
                SetupPaperDispenser(baseDrawing);
            }
            else
            {
                Debug.LogWarning("[Simulador] No se encontró el GameObject 'Dibujo' o 'Dibujo_1' en la escena activa.");
                SetupPaperDispenser(null);
            }


            // 5. EventSystem
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
                Debug.Log("<color=green>[Simulador]</color> EventSystem configurado con InputSystemUIInputModule.");
            }

            // 6. Controlador de Jugador FirstPersonPlayerController en la cámara
            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonPlayerController>() == null)
            {
                cam.gameObject.AddComponent<FirstPersonPlayerController>();
                Debug.Log("<color=green>[Simulador]</color> FirstPersonPlayerController añadido a la cámara principal.");
            }

            // 7. Administrador de Realidad Virtual (XRPlayerRigManager)
            if (Object.FindAnyObjectByType<XRPlayerRigManager>() == null)
            {
                GameObject xrManagerObj = new GameObject("XRPlayerRigManager");
                xrManagerObj.AddComponent<XRPlayerRigManager>();
                Debug.Log("<color=green>[Simulador]</color> XRPlayerRigManager añadido a la escena.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=cyan><b>[Simulador] ¡Configuración completada! Ya puedes pulsar Play para probar todo el flujo.</b></color>");
        }

        private static void ConfigureDrawingSheet(GameObject sheet, string title, Vector3 position)
        {
            sheet.transform.position = position;

            var scannable = sheet.GetComponent<ScannableDrawing>();
            if (scannable == null) scannable = sheet.AddComponent<ScannableDrawing>();

            var box = sheet.GetComponent<BoxCollider>();
            if (box == null) box = sheet.AddComponent<BoxCollider>();
            box.size = new Vector3(10f, 0.5f, 10f);

            var canvas = sheet.GetComponent<DrawingCanvas>();
            if (canvas == null) canvas = sheet.AddComponent<DrawingCanvas>();
        }

        private static void SetupPaperDispenser(GameObject baseDrawing)
        {
            if (GameObject.Find("PilaDeHojas") != null) return;

            GameObject d3 = GameObject.Find("Dibujo_3") ?? baseDrawing ?? GameObject.Find("Dibujo_1");
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
            if (trayCol != null) Object.DestroyImmediate(trayCol);

            Renderer tr = tray.GetComponent<Renderer>();
            if (tr != null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material tMat = new Material(litShader);
                tMat.color = new Color(0.20f, 0.16f, 0.12f);
                if (tMat.HasProperty("_BaseColor")) tMat.SetColor("_BaseColor", new Color(0.20f, 0.16f, 0.12f));
                tr.material = tMat;
            }

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
#endif
