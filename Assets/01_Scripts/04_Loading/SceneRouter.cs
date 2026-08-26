using UnityEngine.SceneManagement;

public static class SceneRouter
{
    public static string LoadingScene = "00_Loading";

    private static string pendingScene;

    public static bool HasPendingScene => !string.IsNullOrEmpty(pendingScene);

    public static void GoThroughLoading(string targetScene)
    {
        pendingScene = targetScene;
        SceneManager.LoadScene(LoadingScene);
    }

    // The loading screen takes the destination and clears it, so a reload never repeats it
    public static string ConsumePendingScene()
    {
        string scene = pendingScene;
        pendingScene = null;
        return scene;
    }
}