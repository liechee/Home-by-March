namespace Unity.Services.Authentication.PlayerAccounts.Samples
{
    public interface ISceneNavigator
    {
        /// <summary>Navigate back to the login screen (Scene 1).</summary>
        void GoToScene1();

        /// <summary>Navigate forward to the main game scene (Scene 2).</summary>
        void GoToScene2();
    }
}