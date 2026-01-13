#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Repforge.StepCounterPro
{
    public class StepCounterPostProcessBuild
    {

        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target == BuildTarget.iOS)
            {
                string plistPath = pathToBuiltProject + "/Info.plist";
                PlistDocument plist = new PlistDocument();
                plist.ReadFromString(File.ReadAllText(plistPath));
                PlistElementDict rootDict = plist.root;
                var configPath = AssetDatabase.FindAssets("t:StepCounterConfig");
                if (configPath.Length > 0)
                {
                    var config = AssetDatabase.LoadAssetAtPath<StepCounterConfig>(AssetDatabase.GUIDToAssetPath(configPath[0]));
                    if (!rootDict.values.ContainsKey("NSMotionUsageDescription") || config.overrideMotionUsageDescription)
                        rootDict.SetString("NSMotionUsageDescription", config.motionUsageDescription);
                }
                else
                {
                    Debug.LogWarning("StepCounter Pro: config file not found");
                }



                // Write to file
                File.WriteAllText(plistPath, plist.WriteToString());
            }
        }

    }
}
#endif