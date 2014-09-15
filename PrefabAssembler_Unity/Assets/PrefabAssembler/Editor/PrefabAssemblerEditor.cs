using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System;

[CustomEditor(typeof(PrefabAssembler))]
[CanEditMultipleObjects]
[InitializeOnLoad]
public class PrefabAssemblerEditor : Editor
{	

	static PrefabAssemblerEditor ()
	{
		EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
		EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
		
		var assembleCallbackField = typeof(PrefabAssembler).GetField("EDITOR_AssembleCallback", BindingFlags.Static | BindingFlags.NonPublic);
		var assembleCallbackDelegate = Delegate.CreateDelegate(typeof(Action<PrefabAssembler>), typeof(PrefabAssemblerUtility), "Assemble");
		assembleCallbackField.SetValue(null, assembleCallbackDelegate);
	}

	static void OnProjectWindowItemGUI (string guid, Rect selectionRect)
	{	
		if(Event.current.type == EventType.MouseDown 
		&& Event.current.button == 0
		&& Event.current.clickCount == 2
		&& selectionRect.Contains(Event.current.mousePosition))
		{
			var path = AssetDatabase.GUIDToAssetPath(guid);
			var file = new FileInfo(path);
			
			if(file.Extension != ".prefab")
			{
				return;
			}
			
			var go = AssetDatabase.LoadAssetAtPath(path, typeof(GameObject));
			
			var labels = AssetDatabase.GetLabels(go);
			for(int i = 0; i < labels.Length; i++)
			{
				var l = labels[i];
				if(l.StartsWith("Stage: "))
				{
					if(EditorApplication.SaveCurrentSceneIfUserWantsTo())
					{
						EditorApplication.OpenScene("Assets/" + l.Replace("Stage: ", ""));
					}
					break;
				}
			}
		}
	}

	static void OnHierarchyWindowItemGUI (int instanceID, Rect selectionRect)
	{
		var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
		if(!go) return;

		var assembler = go.GetComponent<PrefabAssembler>();
		if(!assembler) return;

		var size = GUI.skin.label.CalcSize(new GUIContent(go.name));
		
		selectionRect.x += size.x;
		
		var label = new GUIContent("[P]");
		var color = new Color(0.15f, 0.15f, 0.15f, 1f);
		if(!assembler.prefab)
		{
			label = new GUIContent("[X]");
			color = new Color(1f, 0.25f, 0.25f, 1f);
		}
		
		size = GUI.skin.label.CalcSize(label);
		selectionRect.width = size.x;

		EditorGUIUtility.AddCursorRect(selectionRect, MouseCursor.Link);

		GUI.color = color;
		GUI.Label(selectionRect, label);
		GUI.color = Color.white;
		if(selectionRect.Contains(Event.current.mousePosition))
		{
			if(Event.current.type == EventType.MouseDown && Event.current.button == 0)
			{
				if(assembler.prefab)
				{
					EditorGUIUtility.PingObject(assembler.prefab);
				}
				else
				{
					BrowsePrefabPath(assembler);
				}
			}
			if((Event.current.type == EventType.MouseDown && Event.current.button == 2)
			|| (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.command))
			{
				if(assembler.prefab)
				{
					PrefabAssemblerUtility.Assemble(new PrefabAssembler[]{assembler});
				}
				else
				{
					BrowsePrefabPath(assembler);
				}
			}
		}
	}
	
	static void BrowsePrefabPath (PrefabAssembler assembler)
	{
		var curPath = 
			assembler.prefab ? AssetDatabase.GetAssetPath(assembler.prefab) 
		: 	!string.IsNullOrEmpty(EditorApplication.currentScene) ? EditorApplication.currentScene
		:	"Assets/";	
		var newPath = EditorUtility.SaveFilePanelInProject("Pick Prefab Path", assembler.name, "prefab", "Pick a location to save the prefab.", curPath);
		if(newPath != null && newPath != "" && newPath != curPath)
		{
			PrefabAssemblerUtility.SetAssemblerTarget(assembler, newPath);
		}
	}

	void OnEnable ()
	{
		while(UnityEditorInternal.ComponentUtility.MoveComponentUp((Component)target));
	}

	public override void OnInspectorGUI ()
	{	
		EditorGUIUtility.fieldWidth = 100;
		EditorGUIUtility.labelWidth = 60;
		
		serializedObject.Update();
	
		var assemblers = new PrefabAssembler[targets.Length];
		for(int i = 0; i < assemblers.Length; i++)
		{
			assemblers[i] = (PrefabAssembler)targets[i];
		}
		
		GUILayout.Space(5);
		
		if(assemblers.Length == 1)
		{
			var assembler = assemblers[0];
			
			EditorGUILayout.BeginHorizontal();
			
			GUILayout.Space(14);
			
			DrawPrefabField(assembler);
			
			if(GUILayout.Button("Browse", GUILayout.Width(60)))
			{
				BrowsePrefabPath(assembler);
			}
			
			EditorGUILayout.EndHorizontal();
			
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(12);
			EditorGUILayout.BeginVertical();
			AdvancedSettings();
			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
		}
		else
		{
			for(int i = 0; i < assemblers.Length; i++)
			{
				var assembler = assemblers[i];
				
				EditorGUILayout.BeginHorizontal();
				
				GUILayout.Space(14);
				
				DrawPrefabField(assembler);
				
				EditorGUILayout.EndHorizontal();
			}
		}
		
		serializedObject.ApplyModifiedProperties();
	}
	
	void DrawPrefabField (PrefabAssembler assembler)
	{
		if(!assembler.prefab)
		{
			EditorGUILayout.LabelField("Target:", "None Assigned");
		}
		else
		{
			EditorGUILayout.ObjectField("Target:", assembler.prefab, typeof(GameObject), false);
		}
	}
	
	void AdvancedSettings ()
	{
		var priority = serializedObject.FindProperty("priority");
		
		EditorGUILayout.BeginHorizontal();
		{
			priority.intValue = EditorGUILayout.IntField("Priority:", priority.intValue);
			
			if(GUILayout.Button("-", GUILayout.Width(23)))
			{
				priority.intValue--;
			}
			
			if(GUILayout.Button("+", GUILayout.Width(23)))
			{
				priority.intValue++;
			}
			
		}
		EditorGUILayout.EndHorizontal();
	}
}