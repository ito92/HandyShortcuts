using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
#endif
using UnityEngine;

namespace DaBois.Utilities
{
    public class HandyShortcuts : ScriptableObject
    {
        [System.Serializable]
        public class ShortcutData
        {
            [SerializeField]
            private string _path = default;
            [SerializeField]
            private Object _asset = default;

            public string Path { get => _path; }
            public Object Asset { get => _asset; }
        }

        [SerializeField]
        private ShortcutData[] _shortcuts = default;

        public static HandyShortcuts Instance => _instance != null ? _instance : Initialize();
        private static HandyShortcuts _instance;

        public ShortcutData[] Shortcuts { get => _shortcuts; }

#if !UNITY_EDITOR
        private void OnEnable()
        {
            RuntimeInit();
        }
#endif
        private const string displayPath = "HandyShortcuts/Settings";
        private const string filename = "HandyShortcutsSettings";
        private const string title = "Handy Shortcuts Settings";
        private readonly string[] tags = new string[] { "Handy", "Shortcut", "Settings" };

        public void RuntimeInit()
        {
            _instance = this;
        }

        public void Refresh()
        {

        }

        protected static HandyShortcuts Initialize()
        {
            if (_instance != null)
            {
                return _instance;
            }

            // Attempt to load the settings asset.
            var path = GetSettingsPath() + filename + ".asset";

            #if UNITY_EDITOR
            _instance = AssetDatabase.LoadAssetAtPath<HandyShortcuts>(path);
            if (_instance != null)
            {
                return _instance;
            }

            //Move asset to the correct path if already exists
            var instances = Resources.FindObjectsOfTypeAll<HandyShortcuts>();
            if (instances.Length > 0)
            {
                var oldPath = AssetDatabase.GetAssetPath(instances[0]);
                var result = AssetDatabase.MoveAsset(oldPath, path);

                if (oldPath == path)
                {
                    Debug.Log(instances[0] + " is in the correct path. Skipping moving");
                    _instance = instances[0];
                    return _instance;
                }
                else if (string.IsNullOrEmpty(result))
                {
                    _instance = instances[0];
                    return _instance;
                }
                else
                {
                    Debug.LogWarning($"Failed to move previous settings asset " + $"'{oldPath}' to '{path}'. " + $"A new settings asset will be created.", _instance);
                }
            }
            if (_instance != null)
            {
                return _instance;
            }
            _instance = CreateInstance<HandyShortcuts>();
            #endif

#if UNITY_EDITOR
            Directory.CreateDirectory(Path.Combine(
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(path)));

            AssetDatabase.CreateAsset(_instance, path);
            AssetDatabase.Refresh();
#endif
            return _instance;
        }

        static string GetSettingsPath()
        {
            return "Assets/Settings/";
        }

        #if UNITY_EDITOR

        private static Editor _editor;

        public SettingsProvider GenerateProvider()
        {
            var provider = new SettingsProvider(displayPath, SettingsScope.Project)
            {
                label = title,
                guiHandler = (searchContext) =>
                {
                    var settings = Instance;

                    if (!_editor)
                    {
                        _editor = Editor.CreateEditor(Instance);
                    }
                    _editor.OnInspectorGUI();
                },

                keywords = tags
            };

            return provider;
        }
        #endif
    }

    #if UNITY_EDITOR
    static class HandyShortcutsRegister
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return HandyShortcuts.Instance.GenerateProvider();
        }
    }

    public class PopUpAssetInspector : EditorWindow
    {
        private Object asset;
        private Editor assetEditor;
        private Vector2 _scroll;
        private Editor[] componentEditors = new Editor[0];

        public static PopUpAssetInspector Create(Object asset)
        {
            var window = CreateWindow<PopUpAssetInspector>($"{asset.name} | {asset.GetType().Name}");
            window.asset = asset;
            window.assetEditor = Editor.CreateEditor(asset);

            if (asset is GameObject)
            {
                Component[] components = ((GameObject)asset).GetComponents<Component>();
                window.componentEditors = new Editor[components.Length];
                for (int i = 0; i < components.Length; i++)
                {
                    window.componentEditors[i] = Editor.CreateEditor(components[i]);
                }
            }

            return window;
        }

        private void OnGUI()
        {
            GUI.enabled = false;
            asset = EditorGUILayout.ObjectField("Asset", asset, asset.GetType(), false);
            GUI.enabled = true;

            _scroll = EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox);
            assetEditor.OnInspectorGUI();

            foreach (var c in componentEditors)
            {
                c.OnInspectorGUI();
            }

            EditorGUILayout.EndScrollView();
        }
    }

    public class FloatingFieldMenu : EditorWindow
    {
        private class MenuNode
        {
            public string Name;
            public string FullPath;
            public HandyShortcuts.ShortcutData Shortcut;
            public Dictionary<string, MenuNode> Children = new Dictionary<string, MenuNode>();

            public bool IsLeaf => Shortcut != null;
        }

        private class MenuEntry
        {
            public string Label;
            public bool IsBack;
            public bool IsFolder;
            public MenuNode Node;
        }

        private static MenuNode root;

        private readonly Stack<MenuNode> navigation = new Stack<MenuNode>();

        private string search = "";
        private Vector2 scroll;
        private int selectedIndex;

        private List<MenuEntry> visibleEntries = new List<MenuEntry>();

        private bool focusSearchNextFrame;

        [MenuItem("Handy Shortcuts/Shortcuts %#k")]
        static void Open()
        {
            var window = GetWindow<FloatingFieldMenu>(true, "Handy Shortcuts");

            float width = 650;
            float height = 450;

            Rect main = EditorGUIUtility.GetMainWindowPosition();


            BuildTree();

            window.navigation.Clear();
            window.navigation.Push(root);

            window.RefreshVisibleEntries();

            window.ShowAsDropDown(new Rect(main.center,Vector2.zero), new Vector2(width,height));
            window.Focus();
        }

        static void BuildTree()
        {
            root = new MenuNode()
            {
                Name = "Root",
                FullPath = ""
            };

            foreach (var shortcut in HandyShortcuts.Instance.Shortcuts)
            {
                if (shortcut.Asset == null || string.IsNullOrWhiteSpace(shortcut.Path))
                {
                    continue;
                }

                string[] split = shortcut.Path.Split('/');

                MenuNode current = root;
                string currentPath = "";

                for (int i = 0; i < split.Length; i++)
                {
                    string part = split[i];

                    currentPath = string.IsNullOrEmpty(currentPath)
                        ? part
                        : currentPath + "/" + part;

                    if (!current.Children.TryGetValue(part, out MenuNode child))
                    {
                        child = new MenuNode()
                        {
                            Name = part,
                            FullPath = currentPath
                        };

                        current.Children.Add(part, child);
                    }

                    current = child;
                }

                current.Shortcut = shortcut;
            }
        }

        void RefreshVisibleEntries()
        {
            visibleEntries.Clear();

            bool searching = !string.IsNullOrWhiteSpace(search);

            if (searching)
            {
                string lower = search.ToLower();

                List<MenuNode> allLeafs = new List<MenuNode>();

                CollectLeafs(root, allLeafs);

                foreach (var node in allLeafs
                    .Where(x =>
                        x.Shortcut.Path.ToLower().Contains(lower) ||
                        x.Shortcut.Asset.name.ToLower().Contains(lower))
                    .OrderBy(x => x.Shortcut.Path))
                {
                    visibleEntries.Add(new MenuEntry()
                    {
                        Label = node.Name,
                        IsFolder = false,
                        Node = node
                    });
                }
            }
            else
            {
                MenuNode current = navigation.Peek();

                if (navigation.Count > 1)
                {
                    visibleEntries.Add(new MenuEntry()
                    {
                        Label = "← Back",
                        IsBack = true
                    });
                }

                IEnumerable<MenuNode> nodes = current.Children.Values;

                foreach (var node in nodes
                    .OrderBy(x => x.IsLeaf)
                    .ThenBy(x => x.Name))
                {
                    visibleEntries.Add(new MenuEntry()
                    {
                        Label = node.Name,
                        IsFolder = !node.IsLeaf,
                        Node = node
                    });
                }
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(visibleEntries.Count - 1, 0));
        }

        void CollectLeafs(MenuNode node, List<MenuNode> result)
        {
            if (node.IsLeaf)
            {
                result.Add(node);
            }

            foreach (var child in node.Children.Values)
            {
                CollectLeafs(child, result);
            }
        }

        private void OnEnable()
        {
            focusSearchNextFrame = true;
        }

        private void OnGUI()
        {
            if (focusSearchNextFrame && Event.current.type == EventType.Repaint)
            {
                focusSearchNextFrame = false;

                EditorGUI.FocusTextInControl("SearchField");

                Repaint();
            }

            HandleKeyboard();

            GUILayout.Space(15);

            DrawBreadcrumbs();

            GUILayout.Space(10);

            GUI.SetNextControlName("SearchField");

            EditorGUI.BeginChangeCheck();

            search = EditorGUILayout.TextField(search, GUILayout.Height(28));

            if (EditorGUI.EndChangeCheck())
            {
                selectedIndex = 0;
                RefreshVisibleEntries();
            }

            GUILayout.Space(10);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < visibleEntries.Count; i++)
            {
                DrawEntry(i, visibleEntries[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawBreadcrumbs()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            string path = string.Join(
                " / ",
                navigation.Reverse().Skip(1).Select(x => x.Name)
            );

            if (string.IsNullOrEmpty(path))
            {
                path = "Root";
            }

            GUILayout.Label(path, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
        }

        void DrawEntry(int index, MenuEntry entry)
        {
            bool selected = index == selectedIndex;

            Rect containerRect = EditorGUILayout.BeginVertical();

            GUILayout.Space(2);

            Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(28));

            if (selected)
            {
                EditorGUI.DrawRect(
                    new Rect(
                        4,
                        rowRect.y - 1,
                        position.width - 8,
                        30
                    ),
                    new Color(.25f, .45f, .9f, .25f)
                );
            }

            if (entry.IsBack)
            {
                GUILayout.Label("↩", GUILayout.Width(22));
                GUILayout.Label(entry.Label, EditorStyles.boldLabel);
            }
            else if (entry.IsFolder)
            {
                GUILayout.Label(
                    EditorGUIUtility.IconContent("Folder Icon").image,
                    GUILayout.Width(20),
                    GUILayout.Height(20)
                );

                GUILayout.Label(entry.Label, EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                GUILayout.Label("›", EditorStyles.boldLabel);
            }
            else
            {
                Texture icon = AssetPreview.GetMiniThumbnail(entry.Node.Shortcut.Asset);

                GUILayout.Label(
                    icon,
                    GUILayout.Width(20),
                    GUILayout.Height(20)
                );

                GUILayout.BeginVertical();

                GUILayout.Label(
                    entry.Node.Shortcut.Asset.name,
                    EditorStyles.boldLabel
                );

                GUILayout.Label(
                    entry.Node.Shortcut.Path,
                    EditorStyles.miniLabel
                );

                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.MouseDown &&
                containerRect.Contains(Event.current.mousePosition))
            {
                ActivateEntry(entry);
            }
        }

        void HandleKeyboard()
        {
            Event e = Event.current;

            if (e.type != EventType.KeyDown)
            {
                return;
            }

            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    selectedIndex++;
                    selectedIndex = Mathf.Min(selectedIndex, visibleEntries.Count - 1);
                    e.Use();
                    Repaint();
                    break;

                case KeyCode.UpArrow:
                    selectedIndex--;
                    selectedIndex = Mathf.Max(selectedIndex, 0);
                    e.Use();
                    Repaint();
                    break;

                case KeyCode.Return:

                    if (visibleEntries.Count > 0)
                    {
                        ActivateEntry(visibleEntries[selectedIndex]);
                    }

                    e.Use();
                    break;

                case KeyCode.LeftArrow:

                    if (navigation.Count > 1)
                    {
                        navigation.Pop();
                        selectedIndex = 0;
                        RefreshVisibleEntries();
                    }

                    e.Use();
                    break;

                case KeyCode.RightArrow:

                    if (visibleEntries.Count > 0)
                    {
                        var entry = visibleEntries[selectedIndex];

                        if (entry.IsFolder)
                        {
                            navigation.Push(entry.Node);
                            selectedIndex = 0;
                            search = "";
                            RefreshVisibleEntries();
                        }
                    }

                    e.Use();
                    break;

                case KeyCode.Escape:
                    Close();
                    e.Use();
                    break;
            }
        }

        void ActivateEntry(MenuEntry entry)
        {
            if (entry.IsBack)
            {
                navigation.Pop();
                selectedIndex = 0;
                search = "";
                RefreshVisibleEntries();
                return;
            }

            if (entry.IsFolder)
            {
                navigation.Push(entry.Node);
                selectedIndex = 0;
                search = "";
                RefreshVisibleEntries();
                return;
            }

            OpenShortcut(entry.Node.Shortcut);
        }

        static void OpenShortcut(HandyShortcuts.ShortcutData shortcut)
        {
            if (shortcut.Asset is SceneAsset)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(
                        AssetDatabase.GetAssetPath(shortcut.Asset)
                    );
                }
            }
            else
            {
                PopUpAssetInspector.Create(shortcut.Asset);
            }

            GetWindow<FloatingFieldMenu>().Close();
        }
    }
    #endif
}