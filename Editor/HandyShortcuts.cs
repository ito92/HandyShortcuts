using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
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
        [MenuItem("Handy Shortcuts/Shortcuts")]
        static void Menu()
        {

            FloatingFieldMenu.Open(new Rect(EditorGUIUtility.GetMainWindowPosition().center, Vector2.one));
        }

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

            if(asset is GameObject)
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

            _scroll =  EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox);
            assetEditor.OnInspectorGUI();

            foreach(var c in componentEditors)
            {
                c.OnInspectorGUI();
            }

            EditorGUILayout.EndScrollView();
        }
    }

    public class FloatingFieldMenu : PopupWindowContent
    {
        private static Rect _position;

        public static void Open(Rect position)
        {
            _position = position;
            PopupWindow.Show(_position, new FloatingFieldMenu());
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUI.BeginChangeCheck();
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 0;

            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < HandyShortcuts.Instance.Shortcuts.Length; i++)
            {
                int id = i;
                menu.AddItem(new GUIContent(HandyShortcuts.Instance.Shortcuts[id].Path), false, () =>
                {
                    if(HandyShortcuts.Instance.Shortcuts[id].Asset is SceneAsset)
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){
                            EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(HandyShortcuts.Instance.Shortcuts[id].Asset));
                        }
                    }
                    else
                    {
                        PopUpAssetInspector.Create(HandyShortcuts.Instance.Shortcuts[id].Asset);
                    }
                });
            }

            menu.ShowAsContext();

            EditorGUIUtility.labelWidth = labelWidth;

            editorWindow.Close();
        }

        public override void OnClose()
        {
            base.OnClose();
        }

        public override Vector2 GetWindowSize()
        {
            return _position.size;
        }
    }

#endif
}