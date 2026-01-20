using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnadoluFethi.StateMachine.Editor
{
    public static class SMCodeGenerator
    {
        private const string StatesFolder = "States";
        private const string StateSuffix = "State";

        public static GenerationResult GenerateStateMachine(SMGenerationData data)
        {
            var result = new GenerationResult();

            if (!ValidateData(data, result))
                return result;

            string basePath = GetFullPath(data.OutputPath, data.SMName);
            string statesPath = Path.Combine(basePath, StatesFolder);

            CreateDirectories(basePath, statesPath);
            GenerateControllerFile(basePath, data);
            GenerateStateFiles(statesPath, data);

            AssetDatabase.Refresh();

            result.Success = true;
            result.OutputPath = $"{data.OutputPath}/{data.SMName}/";
            return result;
        }

        public static GenerationResult GenerateSingleState(string smPath, string smName, string stateName)
        {
            var result = new GenerationResult();
            string formattedStateName = FormatStateName(stateName);
            string statesPath = Path.Combine(GetFullPath(smPath), StatesFolder);

            if (!Directory.Exists(statesPath))
                Directory.CreateDirectory(statesPath);

            string filePath = Path.Combine(statesPath, $"{formattedStateName}.cs");

            if (File.Exists(filePath))
            {
                result.ErrorMessage = $"State '{formattedStateName}' already exists!";
                return result;
            }

            var data = new SMGenerationData
            {
                SMName = smName,
                RootNamespace = SMGeneratorSettings.Instance.RootNamespace
            };

            string content = GenerateStateContent(formattedStateName, data);
            File.WriteAllText(filePath, content);

            AssetDatabase.Refresh();

            result.Success = true;
            result.OutputPath = $"{smPath}/{StatesFolder}/{formattedStateName}.cs";
            result.GeneratedStateName = formattedStateName;
            return result;
        }

        private static bool ValidateData(SMGenerationData data, GenerationResult result)
        {
            if (string.IsNullOrWhiteSpace(data.SMName))
            {
                result.ErrorMessage = "SM Name cannot be empty!";
                return false;
            }

            if (data.StateNames == null || data.StateNames.Count == 0)
            {
                result.ErrorMessage = "Add at least one state!";
                return false;
            }

            return true;
        }

        private static void CreateDirectories(string basePath, string statesPath)
        {
            Directory.CreateDirectory(basePath);
            Directory.CreateDirectory(statesPath);
        }

        private static void GenerateControllerFile(string basePath, SMGenerationData data)
        {
            string content = GenerateControllerContent(data);
            string filePath = Path.Combine(basePath, $"{data.SMName}Controller.cs");
            File.WriteAllText(filePath, content);
        }

        private static void GenerateStateFiles(string statesPath, SMGenerationData data)
        {
            foreach (var stateName in data.StateNames)
            {
                string formattedName = FormatStateName(stateName);
                string content = GenerateStateContent(formattedName, data);
                string filePath = Path.Combine(statesPath, $"{formattedName}.cs");
                File.WriteAllText(filePath, content);
            }
        }

        private static string GenerateControllerContent(SMGenerationData data)
        {
            var formattedStates = GetFormattedStateNames(data.StateNames);

            string content = SMTemplates.ControllerTemplate
                .Replace("{ROOT_NAMESPACE}", data.RootNamespace)
                .Replace("{SM_NAME}", data.SMName)
                .Replace("{STATE_FIELDS}", GenerateStateFields(formattedStates))
                .Replace("{STATE_INITIALIZATIONS}", GenerateStateInitializations(formattedStates))
                .Replace("{STATE_REGISTRATIONS}", GenerateStateRegistrations(formattedStates))
                .Replace("{INITIAL_STATE}", formattedStates[0]);

            return content;
        }

        private static string GenerateStateContent(string stateName, SMGenerationData data)
        {
            return SMTemplates.StateTemplate
                .Replace("{ROOT_NAMESPACE}", data.RootNamespace)
                .Replace("{SM_NAME}", data.SMName)
                .Replace("{STATE_NAME}", stateName);
        }

        private static string GenerateStateFields(List<string> stateNames)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < stateNames.Count; i++)
            {
                string line = SMTemplates.StateFieldTemplate
                    .Replace("{STATE_NAME}", stateNames[i])
                    .Replace("{FIELD_NAME}", GetFieldName(stateNames[i]));

                sb.Append(line);
                if (i < stateNames.Count - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string GenerateStateInitializations(List<string> stateNames)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < stateNames.Count; i++)
            {
                string line = SMTemplates.StateInitTemplate
                    .Replace("{STATE_NAME}", stateNames[i])
                    .Replace("{FIELD_NAME}", GetFieldName(stateNames[i]));

                sb.Append(line);
                if (i < stateNames.Count - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string GenerateStateRegistrations(List<string> stateNames)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < stateNames.Count; i++)
            {
                string line = SMTemplates.StateRegisterTemplate
                    .Replace("{FIELD_NAME}", GetFieldName(stateNames[i]));

                sb.Append(line);
                if (i < stateNames.Count - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }

        private static List<string> GetFormattedStateNames(List<string> stateNames)
        {
            var formatted = new List<string>();
            foreach (var name in stateNames)
            {
                formatted.Add(FormatStateName(name));
            }
            return formatted;
        }

        public static string FormatStateName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return StateSuffix;

            return name.EndsWith(StateSuffix) ? name : name + StateSuffix;
        }

        private static string GetFieldName(string stateName)
        {
            return "_" + char.ToLower(stateName[0]) + stateName.Substring(1);
        }

        private static string GetFullPath(string relativePath, string smName = "")
        {
            string basePath = Application.dataPath.Replace("Assets", "");
            return string.IsNullOrEmpty(smName)
                ? Path.Combine(basePath, relativePath)
                : Path.Combine(basePath, relativePath, smName);
        }
    }

    public class SMGenerationData
    {
        public string SMName { get; set; }
        public string OutputPath { get; set; }
        public string RootNamespace { get; set; }
        public List<string> StateNames { get; set; }
    }

    public class GenerationResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; }
        public string ErrorMessage { get; set; }
        public string GeneratedStateName { get; set; }
    }
}
