using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using XNode;
using Object = UnityEngine.Object;

namespace XNodeEditor
{
    [InitializeOnLoad]
    public partial class NodeEditorWindow : EditorWindow
    {
        public static NodeEditorWindow current;

        /// <summary> Stores node positions for all nodePorts. </summary>
        public Dictionary<NodePort, Rect> portConnectionPoints { get; } = new Dictionary<NodePort, Rect>();
        [SerializeField] private NodePortReference[] _references = new NodePortReference[0];
        [SerializeField] private Rect[] _rects = new Rect[0];

        private Func<bool> isDocked
        {
            get
            {
                if (_isDocked == null)
                {
                    _isDocked = this.GetIsDockedDelegate();
                }

                return _isDocked;
            }
        }
        private Func<bool> _isDocked;

        [Serializable] private class NodePortReference
        {
            [SerializeField] private Node _node;
            [SerializeField] private string _name;

            public NodePortReference(NodePort nodePort)
            {
                _node = nodePort.node;
                _name = nodePort.fieldName;
            }

            public NodePort GetNodePort()
            {
                if (_node == null)
                {
                    return null;
                }

                return _node.GetPort(_name);
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.playModeStateChanged += PlaymodeStateChanged;

            // Reload portConnectionPoints if there are any
            int length = _references.Length;
            if (length == _rects.Length)
            {
                for (int i = 0; i < length; i++)
                {
                    NodePort nodePort = _references[i].GetNodePort();
                    if (nodePort != null)
                    {
                        portConnectionPoints.Add(nodePort, _rects[i]);
                    }
                }
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.playModeStateChanged -= PlaymodeStateChanged;

            // Cache portConnectionPoints before serialization starts
            int count = portConnectionPoints.Count;
            _references = new NodePortReference[count];
            _rects = new Rect[count];
            int index = 0;
            foreach (var portConnectionPoint in portConnectionPoints)
            {
                _references[index] = new NodePortReference(portConnectionPoint.Key);
                _rects[index] = portConnectionPoint.Value;
                index++;
            }
        }

        private void PlaymodeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.EnteredEditMode)
            {
                editModeEntered = true;
            }
            else if (playModeStateChange == PlayModeStateChange.ExitingPlayMode)
            {
                editModeEntered = false;
            }
        }

        public Dictionary<Node, Vector2> nodeSizes { get; } = new Dictionary<Node, Vector2>();
        public NodeGraph graph;
        public int graphInstanceID;
        public Vector2 panOffset
        {
            get => _panOffset;
            set
            {
                _panOffset = value;
                Repaint();
            }
        }
        private Vector2 _panOffset;
        public float zoom
        {
            get => _zoom;
            set
            {
                _zoom = Mathf.Clamp(value, NodeEditorPreferences.GetSettings().minZoom,
                    NodeEditorPreferences.GetSettings().maxZoom);
                Repaint();
            }
        }
        private float _zoom = 1;

        private void OnFocus()
        {
            current = this;
            ValidateGraphEditor();
            if (graphEditor != null)
            {
                graphEditor.OnWindowFocus();
                if (NodeEditorPreferences.GetSettings().autoSave)
                {
                    AssetDatabase.SaveAssets();
                }
            }

            // dragThreshold = Math.Max(1f, Screen.width / 1000f);
        }

        private void OnLostFocus()
        {
            if (graphEditor != null)
            {
                graphEditor.OnWindowFocusLost();
            }
        }

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary> Handle Selection Change events</summary>
        private static void OnSelectionChanged()
        {
            NodeGraph nodeGraph = Selection.activeObject as NodeGraph;
            if (nodeGraph && !AssetDatabase.Contains(nodeGraph))
            {
                if (NodeEditorPreferences.GetSettings().openOnCreate)
                {
                    Open(nodeGraph);
                }
            }
        }

        private void OnUndoRedoPerformed()
        {
            Repaint();
        }

        /// <summary> Make sure the graph editor is assigned and to the right object </summary>
        private void ValidateGraphEditor()
        {
            NodeGraphEditor graphEditor = NodeGraphEditor.GetEditor(graph, this);
            if (this.graphEditor != graphEditor && graphEditor != null)
            {
                this.graphEditor = graphEditor;
                graphEditor.OnOpen();
            }
        }

        /// <summary> Create editor window </summary>
        public static NodeEditorWindow Init()
        {
            NodeEditorWindow w = CreateInstance<NodeEditorWindow>();
            w.titleContent = new GUIContent("xNode");
            w.wantsMouseMove = true;
            w.Show();
            return w;
        }

        public void Save()
        {
            if (AssetDatabase.Contains(graph))
            {
                EditorUtility.SetDirty(graph);
                if (NodeEditorPreferences.GetSettings().autoSave)
                {
                    AssetDatabase.SaveAssets();
                }
            }
            else
            {
                SaveAs();
            }
        }

        public void SaveAs()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save NodeGraph", "NewNodeGraph", "asset", "");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            NodeGraph existingGraph = AssetDatabase.LoadAssetAtPath<NodeGraph>(path);
            if (existingGraph != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(graph, path);
            EditorUtility.SetDirty(graph);
            if (NodeEditorPreferences.GetSettings().autoSave)
            {
                AssetDatabase.SaveAssets();
            }
        }

        private void DraggableWindow(int windowID)
        {
            GUI.DragWindow();
        }

        public Vector2 WindowToGridPosition(Vector2 windowPosition)
        {
            return (windowPosition - position.size * 0.5f - panOffset / zoom) * zoom;
        }

        public Vector2 GridToWindowPosition(Vector2 gridPosition)
        {
            return position.size * 0.5f + panOffset / zoom + gridPosition / zoom;
        }

        public Rect GridToWindowRectNoClipped(Rect gridRect)
        {
            gridRect.position = GridToWindowPositionNoClipped(gridRect.position);
            return gridRect;
        }

        public Rect GridToWindowRect(Rect gridRect)
        {
            gridRect.position = GridToWindowPosition(gridRect.position);
            gridRect.size /= zoom;
            return gridRect;
        }

        public Vector2 GridToWindowPositionNoClipped(Vector2 gridPosition)
        {
            Vector2 center = position.size * 0.5f;
            float xOffset = center.x * zoom + (panOffset.x + gridPosition.x);
            float yOffset = center.y * zoom + (panOffset.y + gridPosition.y);
            return new Vector2(xOffset, yOffset);
        }

        public void SelectNode(Node node, bool add)
        {
            if (add)
            {
                var selection = new List<Object>(Selection.objects);
                selection.Add(node);
                Selection.objects = selection.ToArray();
            }
            else
            {
                Selection.objects = new Object[] { node };
            }
        }

        public void DeselectNode(Node node)
        {
            var selection = new List<Object>(Selection.objects);
            selection.Remove(node);
            Selection.objects = selection.ToArray();
        }

        [OnOpenAsset(0)]
        public static bool OnOpen(int instanceID, int line)
        {
            NodeGraph nodeGraph = EditorUtility.InstanceIDToObject(instanceID) as NodeGraph;
            if (nodeGraph != null)
            {
                Open(nodeGraph);
                return true;
            }

            return false;
        }

        /// <summary>Open the provided graph in the NodeEditor</summary>
        public static NodeEditorWindow Open(NodeGraph graph)
        {
            if (!graph)
            {
                return null;
            }

            NodeEditorWindow w = GetWindow(typeof(NodeEditorWindow), false, "xNode", true) as NodeEditorWindow;
            w.wantsMouseMove = true;
            w.graph = graph;
            w.graphInstanceID = graph.GetInstanceID();

            return w;
        }

        /// <summary> Repaint all open NodeEditorWindows. </summary>
        public static void RepaintAll()
        {
            var windows = Resources.FindObjectsOfTypeAll<NodeEditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                windows[i].Repaint();
            }
        }
    }
}