using UnityEngine;
using UnityEditor;

namespace AnadoluFethi.StateMachine.Editor
{
    [CreateAssetMenu(fileName = "SMGeneratorSettings", menuName = "Anadolu Fethi/SM Generator Settings")]
    public class SMGeneratorSettings : ScriptableObject
    {
        private const string SettingsPath = "Assets/-SCRIPT/StateMachine/Editor/Config/SMGeneratorSettings.asset";

        [Header("Namespace")]
        [SerializeField] private string _rootNamespace = "AnadoluFethi";

        [Header("Default Paths")]
        [SerializeField] private string _defaultOutputPath = "Assets/-SCRIPT";

        [Header("Window Settings")]
        [SerializeField] private Vector2 _minWindowSize = new Vector2(400, 500);
        [SerializeField] private float _scrollViewHeight = 200f;

        [Header("UI Settings")]
        [SerializeField] private float _labelWidth = 80f;
        [SerializeField] private float _buttonSmallWidth = 30f;
        [SerializeField] private float _buttonHeight = 40f;
        [SerializeField] private int _headerFontSize = 16;

        [Header("Colors")]
        [SerializeField] private Color _generateButtonColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _addStateButtonColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color _removeButtonColor = Color.red;

        // Properties
        public string RootNamespace => _rootNamespace;
        public string DefaultOutputPath => _defaultOutputPath;
        public Vector2 MinWindowSize => _minWindowSize;
        public float ScrollViewHeight => _scrollViewHeight;
        public float LabelWidth => _labelWidth;
        public float ButtonSmallWidth => _buttonSmallWidth;
        public float ButtonHeight => _buttonHeight;
        public int HeaderFontSize => _headerFontSize;
        public Color GenerateButtonColor => _generateButtonColor;
        public Color AddStateButtonColor => _addStateButtonColor;
        public Color RemoveButtonColor => _removeButtonColor;

        private static SMGeneratorSettings _instance;

        public static SMGeneratorSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<SMGeneratorSettings>(SettingsPath);

                    if (_instance == null)
                    {
                        _instance = CreateInstance<SMGeneratorSettings>();
                        EnsureDirectoryExists(SettingsPath);
                        AssetDatabase.CreateAsset(_instance, SettingsPath);
                        AssetDatabase.SaveAssets();
                    }
                }
                return _instance;
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                string[] folders = directory.Split('/');
                string currentPath = folders[0];

                for (int i = 1; i < folders.Length; i++)
                {
                    string newPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = newPath;
                }
            }
        }
    }
}
