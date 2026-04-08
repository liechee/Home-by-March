namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    /// <summary>
    /// Implement this interface on your custom SceneManager MonoBehaviour,
    /// then drag it into the m_SceneNavigatorMono slot on Scene1LoginUI and Scene2AuthUI.
    ///
    /// Example implementation using a simple fade transition:
    ///
    ///     public class MySceneManager : MonoBehaviour, ISceneNavigator
    ///     {
    ///         public void GoToScene1() => StartCoroutine(FadeAndLoad("Scene1"));
    ///         public void GoToScene2() => StartCoroutine(FadeAndLoad("Scene2"));
    ///
    ///         private IEnumerator FadeAndLoad(string sceneName)
    ///         {
    ///             // ... your fade-out logic ...
    ///             SceneManager.LoadScene(sceneName);
    ///         }
    ///     }
    /// </summary>
    public interface ISceneNavigator
    {
        /// <summary>Navigate back to the login screen (Scene 1).</summary>
        void GoToScene1();

        /// <summary>Navigate forward to the main game scene (Scene 2).</summary>
        void GoToScene2();
    }
}