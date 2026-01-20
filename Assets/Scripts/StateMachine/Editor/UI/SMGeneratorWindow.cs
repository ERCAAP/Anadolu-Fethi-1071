using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace AnadoluFethi.StateMachine.Editor
{
    public class SMGeneratorWindow : EditorWindow
    {
        private enum Tab { NewSM, AddState }

        private Tab _currentTab;
        private Vector2 _scrollPosition;
        private GUIStyle _headerStyle;

        // New SM Tab
        private string _smName = "NewSM";
        private List<string> _stateNames = new List<string>();
        private string _selectedPath;

        // Add State Tab
        private string _existingSMPath = "";
        private string _existingSMName = "";
        private string _newStateName = "NewState";

        private SMGeneratorSettings Settings => SMGeneratorSettings.Instance;

        [MenuItem("Tools/Anadolu Fethi/State Machine Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<SMGeneratorWindow>("SM Generator");
            window.minSize = SMGeneratorSettings.Instance.MinWindowSize;
        }

        private void OnEnable()
        {
            _selectedPath = Settings.DefaultOutputPath;
            InitializeDefaultStates();
        }

        private void InitializeDefaultStates()
        {
            if (_stateNames.Count == 0)
            {
                _stateNames.Add("Idle");
                _stateNames.Add("Move");
            }
        }

        private void OnGUI()
        {
            InitializeStyles();
            DrawTabBar();
            EditorGUILayout.Space(10);

            switch (_currentTab)
            {
                case Tab.NewSM:
                    DrawNewSMTab();
                    break;
                case Tab.AddState:
                    DrawAddStateTab();
                    break;
            }
        }

        private void InitializeStyles()
        {
            _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Settings.HeaderFontSize,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void DrawTabBar()
        {
            EditorGUILayout.Space(10);
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab,
                new[] { "New SM", "Add State" },
                GUILayout.Height(30));
        }

        #region New SM Tab

        private void DrawNewSMTab()
        {
            EditorGUILayout.LabelField("Create New State Machine", _headerStyle);
            EditorGUILayout.Space(15);

            DrawSMNameField();
            EditorGUILayout.Space(10);
            DrawPathField();
            EditorGUILayout.Space(15);
            DrawStatesList();
            EditorGUILayout.Space(15);
            DrawPreview();
            EditorGUILayout.Space(15);
            DrawGenerateButton();
        }

        private void DrawSMNameField()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("SM Name:", GUILayout.Width(Settings.LabelWidth));
            _smName = EditorGUILayout.TextField(_smName);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPathField()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Path:", GUILayout.Width(Settings.LabelWidth));
            EditorGUILayout.TextField(_selectedPath);

            if (GUILayout.Button("...", GUILayout.Width(Settings.ButtonSmallWidth)))
            {
                SelectFolder(ref _selectedPath);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatesList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("States:", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+", GUILayout.Width(Settings.ButtonSmallWidth)))
            {
                _stateNames.Add("New");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition,
                GUILayout.Height(Settings.ScrollViewHeight));

            DrawStateItems();

            EditorGUILayout.EndScrollView();
        }

        private void DrawStateItems()
        {
            for (int i = 0; i < _stateNames.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _stateNames[i] = EditorGUILayout.TextField(_stateNames[i]);

                GUI.backgroundColor = Settings.RemoveButtonColor;
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    _stateNames.RemoveAt(i);
                    i--;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(BuildPreviewText(), MessageType.Info);
        }

        private string BuildPreviewText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Folder: {_selectedPath}/{_smName}/");
            sb.AppendLine("Files:");
            sb.AppendLine($"  - {_smName}Controller.cs");
            sb.AppendLine("  - States/");

            foreach (var state in _stateNames)
            {
                sb.AppendLine($"    - {SMCodeGenerator.FormatStateName(state)}.cs");
            }

            return sb.ToString().TrimEnd();
        }

        private void DrawGenerateButton()
        {
            GUI.backgroundColor = Settings.GenerateButtonColor;
            if (GUILayout.Button("Generate State Machine", GUILayout.Height(Settings.ButtonHeight)))
            {
                GenerateStateMachine();
            }
            GUI.backgroundColor = Color.white;
        }

        private void GenerateStateMachine()
        {
            var data = new SMGenerationData
            {
                SMName = _smName,
                OutputPath = _selectedPath,
                RootNamespace = Settings.RootNamespace,
                StateNames = new List<string>(_stateNames)
            };

            var result = SMCodeGenerator.GenerateStateMachine(data);

            if (result.Success)
            {
                EditorUtility.DisplayDialog("Success",
                    $"State Machine '{_smName}' generated!\n\nPath: {result.OutputPath}",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", result.ErrorMessage, "OK");
            }
        }

        #endregion

        #region Add State Tab

        private void DrawAddStateTab()
        {
            EditorGUILayout.LabelField("Add State to Existing SM", _headerStyle);
            EditorGUILayout.Space(15);

            DrawSMFolderField();
            EditorGUILayout.Space(20);
            DrawNewStateField();
            EditorGUILayout.Space(10);
            DrawInstructions();
            EditorGUILayout.Space(20);
            DrawAddStatePreview();
            EditorGUILayout.Space(15);
            DrawAddStateButton();
        }

        private void DrawSMFolderField()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("SM Folder:", GUILayout.Width(Settings.LabelWidth));

            GUI.enabled = false;
            EditorGUILayout.TextField(string.IsNullOrEmpty(_existingSMPath)
                ? "Select SM folder..."
                : _existingSMPath);
            GUI.enabled = true;

            if (GUILayout.Button("...", GUILayout.Width(Settings.ButtonSmallWidth)))
            {
                SelectSMFolder();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_existingSMName))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Detected SM: {_existingSMName}", EditorStyles.miniLabel);
            }
        }

        private void SelectSMFolder()
        {
            string path = EditorUtility.OpenFolderPanel("Select State Machine Folder",
                Settings.DefaultOutputPath, "");

            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                _existingSMPath = "Assets" + path.Substring(Application.dataPath.Length);
                _existingSMName = Path.GetFileName(path);
            }
        }

        private void DrawNewStateField()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("State Name:", GUILayout.Width(Settings.LabelWidth));
            _newStateName = EditorGUILayout.TextField(_newStateName);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInstructions()
        {
            EditorGUILayout.HelpBox(
                "Steps:\n" +
                "1. Select your SM folder\n" +
                "2. Enter state name\n" +
                "3. Click 'Add State'\n\n" +
                "Note: Register the state manually in Controller.",
                MessageType.Info);
        }

        private void DrawAddStatePreview()
        {
            if (string.IsNullOrEmpty(_existingSMName))
                return;

            string formattedName = SMCodeGenerator.FormatStateName(_newStateName);
            EditorGUILayout.LabelField("Will Create:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  {_existingSMPath}/States/{formattedName}.cs",
                EditorStyles.miniLabel);
        }

        private void DrawAddStateButton()
        {
            bool canAdd = !string.IsNullOrEmpty(_existingSMPath) &&
                          !string.IsNullOrEmpty(_newStateName);

            GUI.enabled = canAdd;
            GUI.backgroundColor = Settings.AddStateButtonColor;

            if (GUILayout.Button("+ Add State", GUILayout.Height(Settings.ButtonHeight)))
            {
                AddState();
            }

            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void AddState()
        {
            var result = SMCodeGenerator.GenerateSingleState(
                _existingSMPath,
                _existingSMName,
                _newStateName);

            if (result.Success)
            {
                ShowAddStateSuccess(result);
                _newStateName = "New";
            }
            else
            {
                EditorUtility.DisplayDialog("Error", result.ErrorMessage, "OK");
            }
        }

        private void ShowAddStateSuccess(GenerationResult result)
        {
            string fieldName = "_" + char.ToLower(result.GeneratedStateName[0]) +
                              result.GeneratedStateName.Substring(1);

            EditorUtility.DisplayDialog("Success",
                $"State '{result.GeneratedStateName}' created!\n\n" +
                $"Path: {result.OutputPath}\n\n" +
                "Register in Controller:\n" +
                $"1. Field: private {result.GeneratedStateName} {fieldName};\n" +
                $"2. Init: {fieldName} = new {result.GeneratedStateName}(_stateMachine, this);\n" +
                $"3. Add: _stateMachine.AddState({fieldName});",
                "OK");
        }

        #endregion

        #region Helpers

        private void SelectFolder(ref string path)
        {
            string selected = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");

            if (!string.IsNullOrEmpty(selected) && selected.StartsWith(Application.dataPath))
            {
                path = "Assets" + selected.Substring(Application.dataPath.Length);
            }
        }

        #endregion
    }
}
