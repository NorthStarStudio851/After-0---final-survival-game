using UnityEngine;
using UnityEngine.InputSystem;

public class OvermapController : MonoBehaviour
{
    [SerializeField] private string mapSceneName = "01_BaseScene";
    [SerializeField] private bool escapeReturnsToMap = true;

    private bool leaving;

    // Hook this to a UI Button, or call it from anywhere on the overmap
    public void ReturnToMap()
    {
        if (leaving) return;

        leaving = true;
        SceneRouter.GoThroughLoading(mapSceneName);
    }

    private void Update()
    {
        if (!escapeReturnsToMap || leaving) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame) ReturnToMap();
    }
}