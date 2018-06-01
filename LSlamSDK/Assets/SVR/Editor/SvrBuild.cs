using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class SvrBuild : MonoBehaviour
{
    static void BuildScene(string[] scenes, string apkDir, string apkName)
    {
        Directory.CreateDirectory(apkDir);

        if(System.IO.File.Exists(apkDir + apkName))
        {
            System.IO.File.SetAttributes(apkDir + apkName, System.IO.File.GetAttributes(apkDir + apkName) & ~FileAttributes.ReadOnly);
        }

        BuildPipeline.BuildPlayer(scenes, apkDir + apkName, BuildTarget.Android, BuildOptions.None);
    }
    //
	[MenuItem( "SVR/Build Project" )]
	static void BuildProject( )
	{
		try
		{
            Debug.Log("Bulding Project!");
#           if UNITY_5
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTarget.Android);
#           else
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
#           endif
            {
                string apkDir = "./Build/Android/";
                string apkName = PlayerSettings.productName.Replace(" ", "_");   

				List<string> scenes = new List<string>();
				if( EditorBuildSettings.scenes != null )
				{
					for(int i=0;i<EditorBuildSettings.scenes.Length;i++)
					{
						if( EditorBuildSettings.scenes[i].enabled )
						{
							scenes.Add(EditorBuildSettings.scenes[i].path);
						}
					}
				}

                //Save off current orientation
                UIOrientation currentOrientation = PlayerSettings.defaultInterfaceOrientation;
                
                //Build lr
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeRight;
                BuildScene(scenes.ToArray(), apkDir, apkName+"-lr.apk");
				
                //Build ll
				PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
				BuildScene(scenes.ToArray(), apkDir, apkName+"-ll.apk");

                //revert back
                PlayerSettings.defaultInterfaceOrientation = currentOrientation;
            }
        }
		catch (IOException e)
		{
			Debug.LogError( e.Message );
		}
	}
}
