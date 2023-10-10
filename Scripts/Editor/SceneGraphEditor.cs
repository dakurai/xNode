using System;
using UnityEditor;
using UnityEngine;
using XNode;

namespace XNodeEditor
{
    [CustomEditor(typeof(SceneGraph), true)]
    public class SceneGraphEditor : Editor
    {
        private SceneGraph _sceneGraph;
        private bool _removeSafely;
        private Type _graphType;

        private GUIStyle _titleStyle;
        private GUIStyle _titleSmallStyle;
        private GUIStyle _titleRenameStyle;

        private bool _renameEnable;
        private string _nameInput;

        private bool _shouldInit = true;

        private const string InputControlName = "nameInput";

        private void OnEnable()
        {
            _sceneGraph = target as SceneGraph;
            Type sceneGraphType = _sceneGraph.GetType();
            if (sceneGraphType == typeof(SceneGraph))
            {
                _graphType = null;
            }
            else
            {
                Type baseType = sceneGraphType.BaseType;
                if (baseType.IsGenericType)
                {
                    _graphType = baseType.GetGenericArguments()[0];
                }
            }
        }

        private void Init()
        {
            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.fontSize = 18;

            _titleSmallStyle = new GUIStyle(_titleStyle);
            _titleSmallStyle.fontSize = 10;
            _titleSmallStyle.normal.textColor = Color.gray;

            _titleRenameStyle = new GUIStyle(GUI.skin.textField);
            _titleRenameStyle.alignment = _titleStyle.alignment;
            _titleRenameStyle.fontStyle = _titleStyle.fontStyle;
            _titleRenameStyle.fontSize = _titleStyle.fontSize;
            _titleRenameStyle.normal.textColor = Color.white;
            _titleRenameStyle.stretchHeight = true;
            _titleRenameStyle.padding = _titleStyle.padding;
        }

        public override void OnInspectorGUI()
        {
            if (_shouldInit)
            {
                Init();
                _shouldInit = false;
            }

            if (_sceneGraph.graph == null)
            {
                if (GUILayout.Button("New graph", GUILayout.Height(40)))
                {
                    if (_graphType == null)
                    {
                        var graphTypes = typeof(NodeGraph).GetDerivedTypes();
                        GenericMenu menu = new GenericMenu();
                        for (int i = 0; i < graphTypes.Length; i++)
                        {
                            Type graphType = graphTypes[i];
                            menu.AddItem(new GUIContent(graphType.Name), false, () => CreateGraph(graphType));
                        }

                        menu.ShowAsContext();
                    }
                    else
                    {
                        CreateGraph(_graphType);
                    }
                }
            }
            else
            {
                EditorGUILayout.Space();
                if (_renameEnable)
                {
                    GUI.SetNextControlName(InputControlName);
                    _nameInput = GUILayout.TextField(_nameInput, _titleRenameStyle);
                    EditorGUI.FocusTextInControl(InputControlName);
                }
                else
                {
                    Vector2 size = _titleStyle.CalcSize(new GUIContent(_sceneGraph.graph.name));

                    EditorGUILayout.LabelField(_sceneGraph.graph.name, _titleStyle, GUILayout.Height(size.y));
                }

                Event e = Event.current;
                switch (e.type)
                {
                    case EventType.MouseDown:
                        if (!_renameEnable && !GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                        {
                            break;
                        }

                        _renameEnable = e.clickCount == 2;
                        _nameInput = _sceneGraph.graph.name;

                        EditorGUIUtility.editingTextField = true;
                        TextEditor textEditor =
                            (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                        textEditor.SelectAll();

                        Repaint();
                        break;
                }

                if (!_renameEnable)
                {
                    EditorGUILayout.LabelField("Double click to rename graph", _titleSmallStyle);
                }


                EditorGUILayout.Space();

                // If input is empty, revert name to default instead
                if (_nameInput == null || _nameInput.Trim() == "")
                {
                    if (e.isKey && e.keyCode == KeyCode.Return)
                    {
                        Close();
                    }
                }
                else
                {
                    if (e.isKey && e.keyCode == KeyCode.Return)
                    {
                        SaveAndClose();
                    }
                }

                if (e.isKey && e.keyCode == KeyCode.Escape)
                {
                    Close();
                }

                if (GUILayout.Button("Open graph", GUILayout.Height(40)))
                {
                    NodeEditorWindow.Open(_sceneGraph.graph);
                }

                if (_removeSafely)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Really remove graph?");
                    GUI.color = new Color(1, 0.8f, 0.8f);
                    if (GUILayout.Button("Remove"))
                    {
                        _removeSafely = false;
                        Undo.RecordObject(_sceneGraph, "Removed graph");
                        _sceneGraph.graph = null;
                    }

                    GUI.color = Color.white;
                    if (GUILayout.Button("Cancel"))
                    {
                        _removeSafely = false;
                    }

                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUI.color = new Color(1, 0.8f, 0.8f);
                    if (GUILayout.Button("Remove graph"))
                    {
                        _removeSafely = true;
                    }

                    GUI.color = Color.white;
                }
            }

            DrawDefaultInspector();
        }

        private void SaveAndClose()
        {
            // Enabled undoing of renaming.
            Undo.RecordObject(_sceneGraph.graph, $"Renamed Node: [{_sceneGraph.graph.name}] -> [{_nameInput}]");

            _sceneGraph.graph.name = _nameInput;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_sceneGraph.graph)))
            {
                AssetDatabase.SetMainObject(_sceneGraph.graph, AssetDatabase.GetAssetPath(_sceneGraph.graph));
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(_sceneGraph.graph));
            }

            Close();
            _sceneGraph.graph.TriggerOnValidate();
        }

        private void Close()
        {
            EditorGUIUtility.editingTextField = false;
            _renameEnable = false;
            Repaint();
        }

        public void CreateGraph(Type type)
        {
            Undo.RecordObject(_sceneGraph, "Create graph");
            _sceneGraph.graph = CreateInstance(type) as NodeGraph;
            _sceneGraph.graph.name = _sceneGraph.name + "-graph";
        }
    }
}