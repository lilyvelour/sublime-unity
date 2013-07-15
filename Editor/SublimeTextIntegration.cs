using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

[InitializeOnLoad]
public static class SublimeTextIntegration
{
    ///////////////////////////
    /// EDIT THE FOLLOWING ////
    ///////////////////////////

    // If true (default), automatically open Sublime Text if it is not already open on sync.
    private const bool autoOpenSublime_Win = true;
	private const bool autoOpenSublime_OSX = true;

    // Path to Sublime Text command line
    private const string sublimePath_Win = @"C:\Program Files\Sublime Text 3\sublime_text.exe";
    private const string sublimePath_OSX = @"/Applications/Sublime Text.app/Contents/SharedSupport/bin/subl";

    // Put all extensions you want to include here
    private static string[] includeExtensions = new[]{"cs", "js", "txt", "shader", "compute", "cginc", "xml"};

    ///////////////////////////
    ///////////////////////////
    ///////////////////////////
	
	static string _projectFilePath;
	static IntPtr _foregroundWindow;

    static SublimeTextIntegration()
    {
        SyncSTProject();
    }

    [MenuItem("Assets/Sync Sublime Text Project", false, 999)]
    static void SyncSTProject()
    {
        string unityDLLPath;

        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            unityDLLPath = EditorApplication.applicationContentsPath.Replace('/', '\\') + "\\Managed\\";
        }
        else
        {
            unityDLLPath = EditorApplication.applicationContentsPath + "/Frameworks/Managed/";
        }

        // Output file string
        StringBuilder outFile = new StringBuilder();

        // Output file location
        string outFolder = Application.dataPath.Substring(0, Application.dataPath.Length - 7);

        // Get folder name for current project
        string projectFolderName = outFolder.Substring(outFolder.LastIndexOf("/") + 1);

        // Add project folders
        outFile.Append("{\n");
        outFile.Append("\t\"folders\":\n");
        outFile.Append("\t[\n");

        DirectoryInfo rootFolder = new DirectoryInfo(outFolder);
        DirectoryInfo[] includeFolders = rootFolder.GetDirectories("Assets");

        for(int n = 0; n < includeFolders.Length; ++n)
        {   
            // Excluded folders (no extensions in any subfolders)
            DirectoryInfo[] excludeFolders =
                            includeFolders[n].GetDirectories("*", SearchOption.AllDirectories).Where(
                                folder =>
                                    {
                                        var files = folder.GetFiles("*", SearchOption.AllDirectories);

                                        for(int i = 0; i < files.Length; ++i)
                                        {
                                            for(int j = 0; j < includeExtensions.Length; ++j)
                                            {
                                                if (files[i].Name.ToLower().EndsWith(includeExtensions[j]))
                                                    return false;
                                            }
                                        }

                                        return true;
                                    }).ToArray();

            outFile.Append("\t\t{\n");
            outFile.Append("\t\t\t\"folder_exclude_patterns\":\n");
            outFile.Append("\t\t\t[\n");

            for(int i = 0; i < excludeFolders.Length; ++i)
            {
                outFile.Append("\t\t\t\t\"" + excludeFolders[i].FullName + "\"");

                if(i != excludeFolders.Length-1)
                {
                    outFile.Append(",");
                }

                outFile.Append("\n");
            }

            // Included files
            outFile.Append("\t\t\t],\n");
            outFile.Append("\t\t\t\"file_include_patterns\":\n");
            outFile.Append("\t\t\t[\n");

            for(int i = 0; i < includeExtensions.Length; ++i)
            {
                outFile.Append("\t\t\t\t\"*." + includeExtensions[i] + "\"");

                if(i != includeExtensions.Length-1)
                {
                    outFile.Append(",");
                }

                outFile.Append("\n");
            }
			
            outFile.Append("\t\t\t],\n");
            outFile.Append("\t\t\t\"path\": \"" + includeFolders[n] + "\"\n");
            outFile.Append("\t\t}");

            if(n != includeFolders.Length-1)
            {
                outFile.Append(",");
            }

            outFile.Append("\n");
        }

        outFile.Append("\t],\n");
        outFile.Append("\n");

        // Add autocompletion assemblies
        outFile.Append("\t\"settings\":\n");
        outFile.Append("\t{\n");
        outFile.Append("\t\t\"completesharp_assemblies\":\n");
        outFile.Append("\t\t[\n");
        outFile.Append("\t\t\t\"" + unityDLLPath + "UnityEngine.dll\",\n");
        outFile.Append("\t\t\t\"" + unityDLLPath + "UnityEditor.dll\",\n");
		
		string dataPath = 
			Application.platform == RuntimePlatform.WindowsEditor ? Application.dataPath.Replace('/', '\\') : Application.dataPath;
		
		string scriptAssembliesPath =
			Application.platform == RuntimePlatform.WindowsEditor ? 
				"\\..\\Library\\ScriptAssemblies\\" :
				"/../Library/ScriptAssemblies/";
		
		outFile.Append("\t\t\t\"" + dataPath + scriptAssembliesPath + "Assembly-CSharp.dll\",\n");
        outFile.Append("\t\t\t\"" + dataPath + scriptAssembliesPath + "Assembly-CSharp-Editor.dll\"");

        string[] dllFiles = Directory.GetFiles(dataPath, "*.dll", SearchOption.AllDirectories);

        if(dllFiles.Length > 0)
        {
            outFile.Append(",\n");
        }
        else
        {
            outFile.Append("\n");
        }

        foreach(string file in dllFiles)
        {
            outFile.Append("\t\t\t\"" + file + "\"");

            if(file != dllFiles[dllFiles.Length-1])
            {
                outFile.Append(",");
            }

            outFile.Append("\n");
        }

        outFile.Append("\t\t]\n");
        outFile.Append("\t}\n");
        outFile.Append("}\n");
		
		string outF;
		
		// Do all string conversion for Windows at the end
		if (Application.platform == RuntimePlatform.WindowsEditor)
		{
			outF = Regex.Replace(outFile.ToString(), @"([a-z])+:\\", "/$1/", RegexOptions.IgnoreCase);
			outF = outF.Replace(@"\", @"/");
		}
		else
		{
			outF = outFile.ToString();
		}
		
        // Write the file to disk
		DirectoryInfo dI = new DirectoryInfo(outFolder);
        _projectFilePath = Path.Combine(dI.FullName, projectFolderName + ".sublime-project");
        StreamWriter sw = new StreamWriter(_projectFilePath);
		sw.Write(outF);
        sw.Close();
		
		bool autoOpenSublime = Application.platform == RuntimePlatform.WindowsEditor ? autoOpenSublime_Win : autoOpenSublime_OSX;

		if (autoOpenSublime)
		{
            OpenSublime();

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                _foregroundWindow = GetForegroundWindow();
			    EditorApplication.update += SetForeground;
	        }
        }	
	}
	
	static void OpenSublime()
	{
		if (_projectFilePath != null)
		{
	        // Open sublime
			string sublimePath = Application.platform == RuntimePlatform.WindowsEditor ? sublimePath_Win : sublimePath_OSX;
            string args = "-a -b --project \"" + _projectFilePath + "\"";

            Process.Start(sublimePath, args);

	        _projectFilePath = null;
		}
    }

    #region Foreground Window helpers [Windows only]
    [DllImport("user32.dll")]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);
    
    static void SetForeground()
    {    
        EditorApplication.update -= SetForeground;

        // retrieve window handle
        IntPtr hWnd = _foregroundWindow;
        if (!hWnd.Equals(IntPtr.Zero))
        {
            SetForegroundWindow(hWnd);
        }

        _foregroundWindow = default(IntPtr);
    }
    #endregion
}