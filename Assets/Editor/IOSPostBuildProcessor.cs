#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class IOSPostBuildProcessor
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);

        string frameworkTarget = project.GetUnityFrameworkTargetGuid();
        project.AddFrameworkToProject(frameworkTarget, "Contacts.framework", false);
        project.AddFrameworkToProject(frameworkTarget, "ContactsUI.framework", false);

        File.WriteAllText(projectPath, project.WriteToString());
        Debug.Log("Added Contacts and ContactsUI frameworks to UnityFramework target.");
    }
}
#endif
