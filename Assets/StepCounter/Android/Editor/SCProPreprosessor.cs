using System.IO;
using UnityEditor.Android;

namespace Repforge.StepCounterPro
{
public class SCProPreprosessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 2;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        AddAndroidX(path);
        AddJetifier(path);
    }

    void AddAndroidX(string path)
    {
        string gradlePropertiesPath = Path.Combine(path, "../gradle.properties");

        if (File.Exists(gradlePropertiesPath))
        {
            var lines = File.ReadAllLines(gradlePropertiesPath);
            bool found = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("android.useAndroidX"))
                {
                    lines[i] = "android.useAndroidX=true";
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                using (StreamWriter sw = File.AppendText(gradlePropertiesPath))
                {
                    sw.WriteLine();
                    sw.WriteLine("android.useAndroidX=true");
                }
            }
            else
            {
                File.WriteAllLines(gradlePropertiesPath, lines);
            }
        }
        else
        {
            using (StreamWriter sw = File.CreateText(gradlePropertiesPath))
            {
                sw.WriteLine("android.useAndroidX=true");
            }
        }
    }

    void AddJetifier(string path)
    {
        string gradlePropertiesPath = Path.Combine(path, "../gradle.properties");

        if (File.Exists(gradlePropertiesPath))
        {
            var lines = File.ReadAllLines(gradlePropertiesPath);
            bool found = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("android.enableJetifier"))
                {
                    lines[i] = "android.enableJetifier=true";
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                using (StreamWriter sw = File.AppendText(gradlePropertiesPath))
                {
                    sw.WriteLine("android.enableJetifier=true");
                }
            }
            else
            {
                File.WriteAllLines(gradlePropertiesPath, lines);
            }
        }
        else
        {
            using (StreamWriter sw = File.CreateText(gradlePropertiesPath))
            {
                sw.WriteLine("android.enableJetifier=true");
            }
        }
    }
}
}