using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Repforge.StepCounterPro { 
public class CustomGradlePostProcessor : IPostGenerateGradleAndroidProject
{
    // Implement the callback order to make sure it runs after the project is generated
    public int callbackOrder => 1;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // The path to the base build.gradle file
        string gradleFilePath = Path.Combine(path, "build.gradle");

        // Check if the build.gradle file exists
        if (File.Exists(gradleFilePath))
        {
            // Read the current content of the build.gradle file
            string gradleContent = File.ReadAllText(gradleFilePath);

            // The dependencies to add
            string dependenciesToAdd = @"
        implementation 'androidx.appcompat:appcompat:1.6.1'
        implementation 'com.google.android.gms:play-services-fitness:21.2.0'";

            // Look for the dependencies section in the Gradle file
            string dependenciesKeyword = "dependencies {";
            int dependenciesIndex = gradleContent.IndexOf(dependenciesKeyword);

            if (dependenciesIndex >= 0)
            {
                // Insert the dependencies right after the 'dependencies {' line
                int insertIndex = dependenciesIndex + dependenciesKeyword.Length;
                gradleContent = gradleContent.Insert(insertIndex, dependenciesToAdd);
            }

            // Write the modified content back to the build.gradle file
            File.WriteAllText(gradleFilePath, gradleContent);

            Debug.Log("Custom dependencies have been added to the Gradle file.");
        }
        else
        {
            Debug.LogError("Failed to find the build.gradle file.");
        }
    }
}
}