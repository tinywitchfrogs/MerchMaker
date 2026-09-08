using UnityEngine;
using UnityEngine.EventSystems;

namespace ArtSimulator
{
    public static class RuntimeSimulatorInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeScene()
        {
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
                if (rb != null) Object.Destroy(rb);
            }

            // 2. Impresora
            GameObject printerObj = GameObject.Find("Impresora");
            if (printerObj != null)
            {
                if (printerObj.GetComponent<PrinterController>() == null)
                    printerObj.AddComponent<PrinterController>();

                Collider col = printerObj.GetComponent<Collider>();
                if (col == null)
                {
                    BoxCollider box = printerObj.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(1.5f, 1.5f, 1.5f);
                }
            }

            // 3. Dibujos Interactivos (Originales y lienzos para pintar)
            GameObject baseDrawing = GameObject.Find("Dibujo") ?? GameObject.Find("Dibujo_1");
            if (baseDrawing != null)
            {
                baseDrawing.name = "Dibujo_1";

                if (GameObject.Find("Dibujo_2") == null)
                {
                    GameObject d2 = Object.Instantiate(baseDrawing, new Vector3(8.85f, 0.5937f, -7.1495f), baseDrawing.transform.rotation);
                    d2.name = "Dibujo_2";
                }

                if (GameObject.Find("Dibujo_3") == null)
                {
                    GameObject d3 = Object.Instantiate(baseDrawing, new Vector3(8.30f, 0.5937f, -7.1495f), baseDrawing.transform.rotation);
                    d3.name = "Dibujo_3";
                }
            }

            System.Collections.Generic.List<GameObject> drawingObjects = new System.Collections.Generic.List<GameObject>();
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
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

            // 6. Controlador de Jugador FirstPersonPlayerController
            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<FirstPersonPlayerController>() == null)
            {
                cam.gameObject.AddComponent<FirstPersonPlayerController>();
            }
        }
    }
}
