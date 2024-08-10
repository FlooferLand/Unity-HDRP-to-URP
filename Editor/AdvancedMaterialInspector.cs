using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Material))]
public class AdvancedMaterialInspector : MaterialEditor {
    private bool showInternalNames = false;
    private string searchString;

    private void DrawButton() {
        GUILayout.Space(10);
        if (GUILayout.Button(showInternalNames ? "Hide internal names" : "Show internal names")) {
            showInternalNames = !showInternalNames;
        }
        GUILayout.Space(5);
    }

    public override void OnInspectorGUI() {
        if (showInternalNames && isVisible && target is Material material) {
            serializedObject.Update();
            
            // Getting and sorting the properties
            var properties = GetMaterialProperties(targets);
            Array.Sort(properties, (a, b) => {
                // Internal ordering
                bool internalA = a.name.StartsWith("__") || a.displayName.StartsWith("__");
                bool internalB = b.name.StartsWith("__") || b.displayName.StartsWith("__");
                if (internalA)
                    return 1;;
                if (internalB)
                    return -1;;
                
                // Normal ordering
                return (a.name.Length + a.displayName.Length) < (b.name.Length + b.displayName.Length) ? -1 : 1;
            });
            
            // Search system
            bool idSearch = false;
            if (searchString is not null && searchString.Length > 0) {
                if (int.TryParse(searchString, out int id)) {  // Search by ID
                    properties = properties.Where(property => {
                        if (id > properties.Length || id < 0) return false;
                        return string.Equals(property.name, material.shader.GetPropertyName(id), StringComparison.CurrentCultureIgnoreCase);
                    }).ToArray();
                    idSearch = true;
                }
                else {
                    string search = searchString.ToLower();
                    properties = properties.Where(property => {
                        string[] fields = {
                            property.name.ToLower(),
                            property.name.ToLower().Replace("color", "colour"),
                            property.name.ToLower().Replace("colour", "color"),
                            property.displayName.ToLower(),
                            property.displayName.ToLower().Replace("color", "colour"),
                            property.displayName.ToLower().Replace("colour", "color"),
                        };
                        bool matches = fields.Any(field => field.Contains(search));
                        return matches;
                    }).ToArray();
                }
            }
            
            // Info text
            searchString = GUILayout.TextField(searchString ?? "", GUILayout.Height(20), GUILayout.MinWidth(40))?.Trim();
            if (searchString is not null && searchString.Length > 0)
                if (idSearch)
                    GUILayout.Label($"Searching for ID {searchString}..");
                else
                    GUILayout.Label($"Searching for \"{searchString}\".. ({properties.Length} found)");
            else
                GUILayout.Label($"Total: {properties.Length} properties");
            DrawButton();
            GUILayout.Space(25);

            // Drawing the properties into the inspector
            foreach (MaterialProperty property in properties) {
                bool hideInInspector = ((property.flags & MaterialProperty.PropFlags.HideInInspector) != 0) || property.displayName.StartsWith('_');

                // Drawing
                EditorGUILayout.BeginHorizontal(new GUIStyle { alignment = TextAnchor.MiddleLeft }, GUILayout.Height(20));
                {
                    // Left-hand label
                    EditorGUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.labelWidth));
                    {
                        var normalStyle = new GUIStyle {
                            normal = {
                                textColor = Color.Lerp(Color.white, Color.black, hideInInspector ? 0.4f : 0.0f)
                            },
                        };
                        var tinyStyle = new GUIStyle {
                            normal = {
                                textColor = Color.Lerp(Color.gray, Color.black, hideInInspector ? 0.3f : 0.1f),
                            },
                            richText = true,
                            fontSize = 10,
                            padding = new RectOffset {
                                top = -4
                            },
                            margin = new RectOffset {
                                top = -4
                            }
                        };
                        var buttonStyle = new GUIStyle(tinyStyle) {
                            hover = new GUIStyleState {
                                textColor = Color.Lerp(Color.white, Color.black, hideInInspector ? 0.2f : 0.0f)
                            }
                        };
                        var buttonContent = new GUIContent {
                            text = $"<b>{property.name}</b>",
                            tooltip = "Click to copy name"
                        };
                        
                        EditorGUILayout.LabelField(property.displayName, normalStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        if (GUILayout.Button(buttonContent, buttonStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight))) {
                            EditorGUIUtility.systemCopyBuffer = property.name;
                        }
                        EditorGUILayout.LabelField($"<i>{property.type}</i>", tinyStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                    EditorGUILayout.EndVertical();

                    // Right-hand value
                    switch (property.type) {
                        case MaterialProperty.PropType.Color:
                            var color = property.colorValue;
                            var tex = new Texture2D(32, 32);
                            for (int y = 0; y < tex.height; y++) {
                                for (int x = 0; x < tex.width; x++) {
                                    var col = (
                                        y < 4
                                            ? new Color(color.a, color.a, color.a, color.a)
                                            : new Color(color.r, color.g, color.b, 1f)
                                    );
                                    tex.SetPixel(x, y, col);
                                }
                            }
                            tex.Apply();

                            string colorInfo =
                                $"({Math.Round(color.r, 2)}, {Math.Round(color.g, 2)}, {Math.Round(color.b, 2)}, {Math.Round(color.a, 2)})";

                            var content = new GUIContent {
                                image = tex,
                                tooltip = colorInfo
                            };
                            var style = new GUIStyle();
                            GUILayout.Box(content, style, GUILayout.Width(80), GUILayout.Height(40));
                            GUILayout.Label(colorInfo, GUILayout.Width(160));
                            break;
                        case MaterialProperty.PropType.Vector:
                            GUILayout.TextField($"X {Math.Round(property.vectorValue.x, 3)}", GUILayout.Width(50), GUILayout.Height(20));
                            GUILayout.TextField($"Y {Math.Round(property.vectorValue.y, 3)}", GUILayout.Width(50), GUILayout.Height(20));
                            GUILayout.TextField($"Z {Math.Round(property.vectorValue.z, 3)}", GUILayout.Width(50), GUILayout.Height(20));
                            GUILayout.TextField($"W {Math.Round(property.vectorValue.w, 3)}", GUILayout.Width(50), GUILayout.Height(20));
                            break;
                        case MaterialProperty.PropType.Range:
                            GUILayout.Label(
                                Math.Round(property.rangeLimits.x, 4).ToString(CultureInfo.InvariantCulture),
                                new GUIStyle { alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } },
                                GUILayout.Width(60)
                            );
                            GUILayout.HorizontalSlider(
                                property.floatValue, 
                                property.rangeLimits.x, 
                                property.rangeLimits.y, 
                                GUILayout.Width(150)
                            );
                            GUILayout.Label(
                                Math.Round(property.rangeLimits.y, 4).ToString(CultureInfo.InvariantCulture),
                                new GUIStyle { alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } },
                                GUILayout.Width(60)
                            );
                            break;
                        case MaterialProperty.PropType.Int:
                            GUILayout.TextField(property.intValue.ToString(), GUILayout.Width(40), GUILayout.Height(20));
                            break;
                        case MaterialProperty.PropType.Float:
                            GUILayout.TextField($"{property.floatValue:0.00}", GUILayout.Width(60), GUILayout.Height(20));
                            break;
                    }

                    // Context menu button to copy property path
                    // if (GUILayout.Button("...", GUILayout.Width(20))) {
                    //     ShowContextMenu(property);
                    // }
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Box("", GUILayout.MinWidth(1000), GUILayout.Height(2));
                GUILayout.Space(5);
            }
            serializedObject.ApplyModifiedProperties();
        } else {
            base.OnInspectorGUI();
            GUILayout.Space(10);
            DrawButton();
        }
    }

    void ShowContextMenu(MaterialProperty property) {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Copy Property Path"), false, () => CopyPropertyPath(property));
        menu.ShowAsContext();
    }

    void CopyPropertyPath(MaterialProperty property) {
        string propertyPath = property.name;
        EditorGUIUtility.systemCopyBuffer = propertyPath;
    }
}
