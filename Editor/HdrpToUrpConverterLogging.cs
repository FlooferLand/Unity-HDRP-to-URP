#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

/**
 * Made by FlooferLand (2024)
 * Do not redistribute as your own for any monetary gain.
 */

#if UNITY_EDITOR
public class HdrpToUrpConverterLogging : EditorWindow {
    public static int selected = 0;
    
    [MenuItem("Tools/Material Converter/Show logger")]
    public static void ShowWindow() {
        GetWindow(typeof(HdrpToUrpConverterLogging));
    }

    private void OnGUI() {
        var infoStyle = new GUIStyle {
            normal = {
                textColor = Color.gray
            },
            wordWrap = true
        };
        var logCategoryStyle = new GUIStyle(EditorStyles.textArea) {
            richText = true,
            wordWrap = true
        };
        var logCategorySelectedStyle = new GUIStyle(logCategoryStyle) {
            normal = {
                textColor = Color.cyan
            },
        };
        var logTextStyle = new GUIStyle(EditorStyles.textArea) {
            richText = true,
            wordWrap = true
        };
        var logSubTextStyle = new GUIStyle(logTextStyle) {
            padding = {
                left = 60
            }
        };
        
        // Window title
        GUILayout.Label("Logging", EditorStyles.largeLabel);
        
        HdrpToUrpConverter.Logging.ExtraLogging = GUILayout.Toggle(HdrpToUrpConverter.Logging.ExtraLogging, "Extra logging");
        GUILayout.Label("Mainly for debugging. It is recommended to keep this off", infoStyle);
        GUILayout.Space(10);

        // Logging grid
        if (HdrpToUrpConverter.Logging.log.Count != 0) {
            Material? currentCategory = null;
            for (int i = 0; i < HdrpToUrpConverter.Logging.log.Count; i++) {
                var entry = HdrpToUrpConverter.Logging.log[i];
                var col = entry.Type switch {
                    Logger.LogType.Info => Color.white,
                    Logger.LogType.Warn => Color.yellow,
                    Logger.LogType.Error => Color.red,
                    _ => throw new ArgumentOutOfRangeException()
                };

                if (entry.Category is { } mat) {
                    if (currentCategory is null) {  // Title
                        bool button = GUILayout.Button($"<b>{entry.Text}</b>", (i == selected) ? logCategorySelectedStyle : logCategoryStyle);
                        if (button) {
                            selected = (selected == i) ? -1 : selected;
                        }
                    } else if (selected == i) {  // Content
                        GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(col)}>{entry.Text}</color>", logSubTextStyle);
                    }
                    currentCategory = mat;
                }
                else {
                    GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(col)}>{entry.Text}</color>", logTextStyle);
                }
            }
        }
        else {
            GUILayout.Label("No logs present.", infoStyle);
            GUILayout.Label("Once you start conversion, the logs will show up here.", infoStyle);
        }
    }
}
#endif

public class Logger {
    public List<LogEntry> log = new();
    public bool ExtraLogging = false;

    public class LogEntry {
        public readonly LogType Type;
        public readonly string Text;
        public Material? Category;
        public LogEntry(LogType type, string text, Material? category) {
            Type = type;
            Text = text;
            Category = category;
        }
    }
    
    public class MaterialLogCategory {
        public readonly string Name;
        public MaterialLogCategory(string name) {
            Name = name;
        }
    }
    
    public enum LogType {
        Info,
        Warn,
        Error
    }

    public void Log(string str) {
        log.Add(new LogEntry(LogType.Info, str, HdrpToUrpConverter.currentMaterialLogCategory));
    }

    public void LogWarning(string str) {
        log.Add(new LogEntry(LogType.Warn, str, HdrpToUrpConverter.currentMaterialLogCategory));
    }

    public void LogError(string str) {
        log.Add(new LogEntry(LogType.Error, str, HdrpToUrpConverter.currentMaterialLogCategory));
    }

    public void LogDebugMoreInfo(string str) {
        if (ExtraLogging) {
            log.Add(new LogEntry(LogType.Info, str, HdrpToUrpConverter.currentMaterialLogCategory));
        }
    }

    public Material SetCategory(Material mat) {
        return mat;
    }

    public void Clear() {
        log.Clear();
    }
}
