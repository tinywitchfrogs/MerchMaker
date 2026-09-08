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

            // 3. Configurar Hojas de Dibujo (Dibujo_1, Dibujo_2, Dibujo_3)
            GameObject baseDrawing = GameObject.Find("Dibujo") ?? GameObject.Find("Dibujo_1");
            if (baseDrawing != null)
            {
                baseDrawing.name = "Dibujo_1";
                ConfigureDrawingSheet(baseDrawing, "Dibujo 1", new Vector3(9.4023f, 0.5937f, -7.1495f));

                // Dibujo 2
                GameObject drawing2 = GameObject.Find("Dibujo_2");
                if (drawing2 == null)
                {
                    drawing2 = Object.Instantiate(baseDrawing, new Vector3(8.85f, 0.5937f, -7.1495f), baseDrawing.transform.rotation);
                    drawing2.name = "Dibujo_2";
                }
                ConfigureDrawingSheet(drawing2, "Dibujo 2", new Vector3(8.85f, 0.5937f, -7.1495f));

                // Dibujo 3
                GameObject drawing3 = GameObject.Find("Dibujo_3");
                if (drawing3 == null)
                {
                    drawing3 = Object.Instantiate(baseDrawing, new Vector3(8.30f, 0.5937f, -7.1495f), baseDrawing.transform.rotation);
                    drawing3.name = "Dibujo_3";
                }
                ConfigureDrawingSheet(drawing3, "Dibujo 3", new Vector3(8.30f, 0.5937f, -7.1495f));

                Debug.Log("<color=green>[Simulador]</color> 3 Hojas de dibujo configuradas con éxito (Dibujo 1, 2 y 3) con soporte para pintura interactiva.");
            }
            else
            {
                Debug.LogWarning("[Simulador] No se encontró el GameObject 'Dibujo' o 'Dibujo_1' en la escena activa.");
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
    }
}
#endif
