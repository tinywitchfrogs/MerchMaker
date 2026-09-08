using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ArtSimulator
{
    [DefaultExecutionOrder(-1000)]
    public class UIInputEnabler : MonoBehaviour
    {
        private void Awake()
        {
            EnsureUIModuleActions();
        }

        private void OnEnable()
        {
            EnsureUIModuleActions();
        }

        private void Start()
        {
            EnsureUIModuleActions();
        }

        private void Update()
        {
            var module = GetComponent<InputSystemUIInputModule>();
            if (module != null && module.actionsAsset != null)
            {
                var uiMap = module.actionsAsset.FindActionMap("UI");
                if (uiMap != null && !uiMap.enabled)
                {
                    uiMap.Enable();
                }
            }
        }

        public static void EnsureUIModuleActions()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es != null)
            {
                var module = es.GetComponent<InputSystemUIInputModule>();
                if (module != null && module.actionsAsset != null)
                {
                    module.actionsAsset.Enable();
                }
            }
        }
    }
}
