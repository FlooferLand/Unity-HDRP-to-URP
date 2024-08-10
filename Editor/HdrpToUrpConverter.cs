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

// TODO: Finish the logging system & categorize logs based on each material

#if UNITY_EDITOR
// ReSharper disable InconsistentNaming
public class HdrpToUrpConverter : EditorWindow {
    private class ShaderProperties {
        // Basic colour
        public static readonly CrossShaderProperty Color = new(both: "_BaseColor", CrossShaderProperty.Type.Color);
        public static readonly CrossShaderProperty AlbedoMap = new(hdrp: "_BaseColorMap", urp: "_BaseMap", CrossShaderProperty.Type.Texture);

        // Properties
        public static readonly CrossShaderProperty Smoothness = new(both: "_Smoothness", CrossShaderProperty.Type.Range);
        public static readonly CrossShaderProperty Metallic = new(both: "_Metallic", CrossShaderProperty.Type.Float);

        // Detail
        public static readonly CrossShaderProperty DetailMap = new(hdrp: "_DetailMap", urp: "_DetailMask", CrossShaderProperty.Type.Texture);
        // public static readonly CrossShaderProperty DetailAlbedoMap = new(hdrp: null, urp: "_DetailAlbedoMap", CrossShaderProperty.Type.Texture);
        // public static readonly CrossShaderProperty DetailAlbedoMapScale = new(hdrp: null, urp: "_DetailAlbedoMapScale", CrossShaderProperty.Type.Float);
        // public static readonly CrossShaderProperty DetailNormalMap = new(hdrp: null, urp: "_DetailNormalMap", CrossShaderProperty.Type.Texture);
        // public static readonly CrossShaderProperty DetailNormalMapScale = new(hdrp: null, urp: "_DetailNormalMapScale", CrossShaderProperty.Type.Float);

        // Normal map
        public static readonly CrossShaderProperty NormalMap = new(hdrp: "_NormalMap", urp: "_BumpMap", CrossShaderProperty.Type.Texture);
        public static readonly CrossShaderProperty NormalIntensity = new(hdrp: "_NormalScale", urp: "_BumpScale", CrossShaderProperty.Type.Float);
        public static readonly CrossShaderProperty HeightMap = new(hdrp: "_HeightMap", urp: "_ParallaxMap", CrossShaderProperty.Type.Texture);

        // Emission
        public static readonly CrossShaderProperty EmissionMap = new(hdrp: "_EmissiveColorMap", urp: "_EmissionMap", CrossShaderProperty.Type.Texture);
        public static readonly CrossShaderProperty EmissionColor = new(hdrp: "_EmissiveColor", urp: "_EmissionColor", CrossShaderProperty.Type.Color);

        public static readonly string HDRP_EmissionToggle = "_UseEmissiveIntensity";
        public static readonly string HDRP_EmissionIntensity = "_EmissiveIntensity";

        // Specular
        public static readonly CrossShaderProperty SpecularColor = new("_SpecularColor", urp: "_SpecColor", CrossShaderProperty.Type.Color);
        public static readonly CrossShaderProperty SpecularMap = new("_SpecularColorMap", urp: "_SpecGlossMap", CrossShaderProperty.Type.Texture);

        // Masks
        public static readonly HdrpMaskRemapper HDRP_MainMask = new("_MaskMap", "_MetallicGlossMap", "_OcclusionMap", DetailMap.urp, "_SpecGlossMap");

        public static readonly string URP_OcclusionStrength = "_OcclusionStrength";
        // private static readonly HdrpMaskRemapper HDRP_DetailMask = new("_DetailMap", "_MetallicGlossMap",  "_OcclusionMap", DetailMap.urp, Smoothness.urp);
        // private static readonly string HDRP_DetailMask = "_DetailMap";

        // Misc
        public static readonly string HDRP_MaterialType = "_MaterialID";
        public static readonly string HDRP_SupportDecals = "_SupportDecals";
    }
    
    /** Doesn't override existing materials; instead, makes new ones */
    public static bool MakeNewMaterial = true;
    
    /** Creates a subfolder for the new unpacked mask textures */
    public static bool PutTexturesInSubfolder = false;

    // Types
    public static Shader ErrorShader = null!;
    public static Shader HdrpLitShader = null!;
    public static Shader HdrpUnlitShader = null!;
    public static Shader UrpLitShader = null!;
    public static Shader UrpUnlitShader = null!;
    
    enum HdrpMaterialType {
        Subsurface = 0,
        Standard = 1,
        Anisotropy = 2,
        Iridescence = 3,
        SpecularColor = 4,
        Translucent = 5
    }

    // Other variables
    public static Logger Logging = new();
    private static bool cancelConversion = false;
    internal static Material? currentMaterialLogCategory = null;

    private void OnEnable() {
        ErrorShader = Shader.Find("Hidden/InternalErrorShader");
        HdrpLitShader = Shader.Find("HDRP/Lit");
        HdrpUnlitShader = Shader.Find("HDRP/Unlit");
        UrpLitShader  = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("URP/Lit");
        UrpUnlitShader  = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("URP/Unlit");
        cancelConversion = false;
    }

    [MenuItem("Tools/Material Converter/Show window")]
    [MenuItem("Window/Material Converter")]
    public static void ShowWindow () {
        GetWindow(typeof(HdrpToUrpConverter));
    }

    private void OnGUI() {
        var infoStyle = new GUIStyle {
            normal = {
                textColor = Color.gray
            }
        };
        
        GUILayout.Label($"{nameof(HdrpToUrpConverter)}.cs", EditorStyles.largeLabel);
        GUILayout.Label("Addon that converts over materials between pipelines", infoStyle);
        GUILayout.Space(15);
        
        MakeNewMaterial = GUILayout.Toggle(MakeNewMaterial, "Make new materials");
        GUILayout.Label("Makes new materials instead of overriding the already existing ones", infoStyle);
        GUILayout.Space(10);
        
        PutTexturesInSubfolder = GUILayout.Toggle(PutTexturesInSubfolder, "Put textures in subfolder");
        GUILayout.Label("Puts the new textures inside a folder relative to the mask texture", infoStyle);
        GUILayout.Space(10);
        
        GUILayout.Space(10);

        GUILayout.Label("I highly recommend backing up your project before attempting a conversion.");
        GUILayout.Space(15);
        GUILayout.Label("While I am responsible for providing a good addon,");
        GUILayout.Label("I am not responsible for your entire project's materials being messed up permanently.");
        GUILayout.Space(10);
        
        if (GUILayout.Button("Start conversion")) {
            ConvertMaterials();
        }
        if (GUILayout.Button("Attempt fix for Hidden/InternalErrorShader")) {
            FixInternalShaderErrors();
        }
        if (GUILayout.Button("Show logging window")) {
            HdrpToUrpConverterLogging.ShowWindow();
        }
    }

    public static void FixInternalShaderErrors() {
        Logging.Clear();
        if (!UrpLitShader) {
            Logging.LogError("Shaders not found. Ensure the URP package is installed.");
            HdrpToUrpConverterLogging.ShowWindow();
            return;
        }

        int converted = 0;
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in materialGUIDs) {
            string pathStr = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(pathStr);
            
            if (mat.shader == ErrorShader) {
                mat.shader = UrpLitShader;
                EditorUtility.SetDirty(mat);
                converted += 1;
            }
        }

        if (converted > 0) {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Logging.Log($"{converted} materials were fixed.");
        } else {
            Logging.Log("No materials were fixed.");  
        }
        
        HdrpToUrpConverterLogging.ShowWindow();
    }

    public static void ConvertMaterials() {
        cancelConversion = false;
        currentMaterialLogCategory = null;
        Logging.Clear();
        if (!UrpLitShader || !UrpUnlitShader) {
            Logging.LogError("Shaders not found. Ensure the URP and HDRP packages are installed.\nThe HDRP package is required, as all HDRP materials use the \"InternalErrorShader\" without it!");
            HdrpToUrpConverterLogging.ShowWindow();
            return;
        }

        int converted = 0;
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in materialGUIDs) {
            string pathStr = AssetDatabase.GUIDToAssetPath(guid);
            var path = new FileInfo(pathStr);
            Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(pathStr);
            if (cancelConversion) break;
            
            if (baseMat.shader == HdrpLitShader || baseMat.shader == HdrpUnlitShader) {
                if (ConvertMaterial(baseMat, baseMatPath:path, isUnlit:baseMat.shader == HdrpUnlitShader) is { } mat) {
                    if (MakeNewMaterial) {
                        string filename = $"{Path.GetFileNameWithoutExtension(pathStr)} (New){Path.GetExtension(pathStr)}";
                        string newPath = Path.Combine(pathStr, "..", filename);
                        if (newPath.StartsWith("Assets")) {
                            Logging.LogDebugMoreInfo($"Created material at path \"{newPath}\"");
                            AssetDatabase.CreateAsset(mat, newPath);
                            converted += 1;
                        } else {
                            Logging.LogDebugMoreInfo($"Skipped material at path \"{newPath}\" because it was not in Assets/");
                        }
                    } else {
                        EditorUtility.CopySerialized(mat, baseMat);
                        EditorUtility.SetDirty(baseMat);
                        Logging.LogDebugMoreInfo($"Got material at path \"{path}\"");
                        converted += 1;
                    }
                }
            }
        }

        if (cancelConversion) {
            Logging.Log("Cancelled material conversion.");
            HdrpToUrpConverterLogging.ShowWindow();
            return;
        }

        if (converted > 0) {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Logging.Log($"{converted} HDRP materials were successfully converted to URP.");
        } else {
            Logging.Log("Found no HDRP materials that could be converted.");  
        }
        
        EditorUtility.ClearProgressBar();
        HdrpToUrpConverterLogging.ShowWindow();
    }

    private static Material? ConvertMaterial(Material baseMat, FileInfo baseMatPath, bool isUnlit) {
        var materialType = (HdrpMaterialType) baseMat.GetInt(ShaderProperties.HDRP_MaterialType);
        var mat = new Material(baseMat) {
            shader = isUnlit ? UrpUnlitShader : UrpLitShader
        };
        currentMaterialLogCategory = Logging.SetCategory(baseMat);
        
        // Transfer over misc properties with the same name
        int miscPropertyCount = ShaderUtil.GetPropertyCount(baseMat.shader);
        for (int i = 0; i < miscPropertyCount; i++) {
            if (cancelConversion) return baseMat;
            string propertyName = ShaderUtil.GetPropertyName(baseMat.shader, i);
            var propertyType = ShaderUtil.GetPropertyType(baseMat.shader, i);
            if (mat.HasProperty(propertyName)) {
                switch (propertyType) {
                    case ShaderUtil.ShaderPropertyType.Color:
                        mat.SetColor(propertyName, baseMat.GetColor(propertyName));
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        mat.SetVector(propertyName, baseMat.GetVector(propertyName));
                        break;
                    case ShaderUtil.ShaderPropertyType.Int:
                        mat.SetInteger(propertyName, baseMat.GetInteger(propertyName));
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        mat.SetFloat(propertyName, baseMat.GetFloat(propertyName));
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        mat.SetTexture(propertyName, baseMat.GetTexture(propertyName));
                        break;
                }
            }

            if (EditorUtility.DisplayCancelableProgressBar("Converting material..", $"Transferring over misc property '{propertyName}' on material '{baseMat.name}'", (float) i / miscPropertyCount)) {
                cancelConversion = true;
            }
        }
        EditorUtility.ClearProgressBar();

        // NOTE: The "Property () already exists with a different type: 0" errors mean the type of the property isn't a texture
        {
            // Transfer over additional HDRP material properties
            var additional = new[] {
                ShaderProperties.Color,
                ShaderProperties.AlbedoMap,
                
                ShaderProperties.Metallic,
                
                ShaderProperties.NormalMap,
                ShaderProperties.NormalIntensity,
                ShaderProperties.HeightMap,
                
                ShaderProperties.SpecularColor,
                ShaderProperties.SpecularMap,
                
                ShaderProperties.EmissionMap,
                ShaderProperties.EmissionColor
            };
            for (int i = 0; i < additional.Length; i++) {
                if (cancelConversion) return baseMat;
                var property = additional[i];
                switch (property.type) {
                    case CrossShaderProperty.Type.Texture:
                        var map = ReadableEditorTexture.Fetch(baseMat, baseMatPath, property.hdrp);
                        if (map is not null) mat.SetTexture(property.urp, map.texture);
                        if (property == ShaderProperties.EmissionMap) {
                            mat.SetFloat(ShaderProperties.HDRP_EmissionToggle, map is not null ? 1f : 0f);
                        }
                        break;
                    case CrossShaderProperty.Type.Range:
                        mat.SetFloat(property.urp, baseMat.GetFloat(property.hdrp));
                        break;
                    case CrossShaderProperty.Type.Float:
                        mat.SetFloat(property.urp, baseMat.GetFloat(property.hdrp));
                        break;
                    case CrossShaderProperty.Type.Int:
                        mat.SetInteger(property.urp, baseMat.GetInteger(property.hdrp));
                        break;
                    case CrossShaderProperty.Type.Color:
                        mat.SetColor(property.urp, baseMat.GetColor(property.hdrp));
                        break;
                    case CrossShaderProperty.Type.Vector:
                        mat.SetVector(property.urp, baseMat.GetVector(property.hdrp));
                        break;
                }
                if (EditorUtility.DisplayCancelableProgressBar("Converting material..", $"Transferring HDRP property '{property.hdrp}' to URP property '{property.urp}' on material '{baseMat.name}'", (float) i / additional.Length)) {
                    cancelConversion = true;
                }
            }
            EditorUtility.ClearProgressBar();

            // Unwrap and add the mask map if needed
            if (EditorUtility.DisplayCancelableProgressBar("Converting material..", $"Unpacking main HDRP mask textures for material '{baseMat.name}'", 0.8f)) {
                cancelConversion = true;
            }
            mat = UnpackMaskMap(baseMat, baseMatPath, ShaderProperties.HDRP_MainMask, mat);
            EditorUtility.ClearProgressBar();
        }

        currentMaterialLogCategory = null;
        return mat;
    }

    private static Material UnpackMaskMap(Material baseMat, FileInfo baseMatPath, HdrpMaskRemapper mask, Material mat) {
        if (ReadableEditorTexture.Fetch(baseMat, baseMatPath, mask.hdrpMask, out string? maskTexturePath) is not { } maskMap || maskTexturePath == null) {
            Logging.LogDebugMoreInfo($"Could not read mask texture at path \"{ShaderProperties.HDRP_MainMask}\"");
            return mat;
        }
        
        int width = maskMap.texture.width;
        int height = maskMap.texture.height;
        Logging.LogDebugMoreInfo($"Found map of {width}x{height} in \"{baseMatPath.FullName}\"");
        
        // Copy textures
        var r = new List<Color>();
        var g = new List<Color>();
        var b = new List<Color>();
        var a = new List<Color>();
        Color[] maskColors;
        try {
            maskColors = maskMap.texture.GetPixels();
        }
        catch (ArgumentException exception) {
            Logging.LogError($"Call to GetPixels failed on mask map \"{maskMap.texture.name}\".\nDoes the HDRP mask texture field exist on its shader?\n\n{exception}");
            return mat;
        }
        for (int y = 0; y < maskMap.texture.height; y++) {
            for (int x = 0; x < maskMap.texture.width; x++) {
                Color color = maskColors[Mathf.Min(x, width - 1) + Mathf.Min(y, height - 1) * width];
                r.Add(new Color(color.r, color.r, color.r));
                g.Add(new Color(color.g, color.g, color.g));
                b.Add(new Color(color.b, color.b, color.b));
                a.Add(new Color(color.a, color.a, color.a));
            }
        }
        
        // Creating the new textures
        if (mask == ShaderProperties.HDRP_MainMask) {
            var metallic = createIndividualMaskTextureAsset(maskTexturePath, width, height, r.ToArray(), "Metallic", Color.black);
            var occlusion = createIndividualMaskTextureAsset(maskTexturePath, width, height, g.ToArray(), "AO", Color.white);
            var detail = createIndividualMaskTextureAsset(maskTexturePath, width, height, b.ToArray(), "Detail", Color.black);
            var smoothness = createIndividualMaskTextureAsset(maskTexturePath, width, height, a.ToArray(), "Smoothness", Color.gray);

            mat.SetFloat(ShaderProperties.URP_OcclusionStrength, occlusion is not null ? 1 : 0);
            
            mat.SetTexture(mask.urpR, metallic);
            mat.SetTexture(mask.urpG, occlusion);
            mat.SetTexture(mask.urpB, detail);
            mat.SetTexture(mask.urpA, smoothness);
            return mat;
        }
        
        Logging.LogError($"Remap implementation not found for HDRP mask at property '{mask.hdrpMask}'");
        return mat;
    }
    
    // TODO: Don't make a texture if its the default colour and return null!
    private static Texture2D? createIndividualMaskTextureAsset(string maskTexturePath, int width, int height, Color[] pixels, string name, Color defaultColor) {
        // Texture might contain useless data!
        int timesFound = pixels.Count(color => color == defaultColor);
        if (timesFound > pixels.Length - 8) {  // JPEG artifacting might make it so white/black textures have non-white/black pixels in it
            return null;
        }

        // Creating the texture
        var texture = new Texture2D(width, height, TextureFormat.RFloat, false);
        texture.SetPixels(pixels);
        texture.Apply();

        // Making the folder path
        string parentPath;
        if (Directory.GetParent(maskTexturePath)?.FullName is { } parent) {
            if (parent.IndexOf("Assets", StringComparison.InvariantCultureIgnoreCase) is var i and > 0) {
                parentPath = parent[i..];
            } else {
                Logging.LogWarning($"Failed to get path for texture \"{maskTexturePath}\"\nRecovery will be attempted, but you should check if it actually worked or if it just placed the new texture in the wrong place.");
                parentPath = parent;
                return null;
            }
        } else {
            Logging.LogWarning($"Failed to get path for texture \"{maskTexturePath}\"");
            return null;
        }
        
        // Making the texture name
        string textureName = Path.GetFileName(maskTexturePath)
            .Replace("_Mask", "")
            .Replace("Mask", "")
            .Replace("_mask", "")
            .Replace("mask", "")
            .Replace("_MADS", "")
            .Replace("_MAODS", "")
            .Replace("MADS", "")
            .Replace("MAODS", "");
        if (textureName.Contains('.')) {
            textureName = textureName.Split('.')[0];
        }
        if (textureName.Contains('_')) {
            textureName = textureName.Split('_')[0];
        }
        if (textureName.EndsWith('_') || textureName.EndsWith('.')) {
            textureName = textureName[..^1];
        }

        string filename = $"{textureName}_{name}.png";
        string folderPath = PutTexturesInSubfolder ? Path.Combine(parentPath, textureName) : parentPath;
        string texturePath = Path.Combine(folderPath, filename);

        if (PutTexturesInSubfolder) {
            if (!AssetDatabase.IsValidFolder(folderPath)) {
                AssetDatabase.CreateFolder(parentPath, textureName);
            }
        }

        // -
        Logging.LogDebugMoreInfo($"Creating sub-texture of mask texture at path \"{texturePath}\"");
        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(texturePath);
        AssetDatabase.Refresh();
        
        return AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;
    }
}
#endif

#if UNITY_EDITOR
public class ReadableEditorTexture : IDisposable {
    protected TextureImporter importer;
    public Texture2D texture;
    protected ReadableEditorTexture(TextureImporter importer, Texture2D texture) {
        this.importer = importer;
        this.texture = texture;
    }

    public static ReadableEditorTexture? Fetch(Material mat, FileInfo matPath, string propertyId) {
        return Fetch(mat, matPath, propertyId, out string? _);
    }

    public static ReadableEditorTexture? Fetch(Material mat, FileInfo matPath, string propertyId, out string? texturePath) {
        string location = $"{nameof(ReadableEditorTexture)}.{nameof(Fetch)}";

        // Loading the texture importer
        TextureImporter importer;
        if (mat.HasTexture(propertyId) && mat.GetTexture(propertyId) is { } tex1) {
            texturePath = AssetDatabase.GetAssetPath(tex1);
            
            if (AssetImporter.GetAtPath(texturePath) is not { } assetImporter) {
                HdrpToUrpConverter.Logging.LogError($"Failed loading importer for asset \"{matPath.FullName}\"");
                return null;
            }
            if (assetImporter is not TextureImporter porter) {
                HdrpToUrpConverter.Logging.LogError($"Failed cast to {nameof(TextureImporter)} for texture \"{propertyId}\" (\"{matPath.FullName}\")");
                return null;
            }
            importer = porter;
        }
        else {
            HdrpToUrpConverter.Logging.LogDebugMoreInfo($"Material \"{mat.name}\" does not have a texture for ID \"{propertyId}\"");
            texturePath = null;
            return null;
        }

        // Loading the texture
        importer.isReadable = true;
        importer.SaveAndReimport();
        if (mat.HasTexture(propertyId) && mat.GetTexture(propertyId) is Texture2D importedTex) {
            return new ReadableEditorTexture(importer, importedTex);
        }

        // Unable to load the texture
        HdrpToUrpConverter.Logging.LogError($"Failed loading texture \"{matPath.FullName}\"\nMaterial does not have a texture for ID \"{propertyId}\"");
        importer.isReadable = false;
        importer.SaveAndReimport();
        return null;
    }
    
    public void Dispose() {
        importer.isReadable = false;
        importer.SaveAndReimport();
    }
}
#endif

public class CrossShaderProperty {
    public readonly string hdrp;
    public readonly string urp;
    public readonly Type type;

    public enum Type {
        Color,
        Vector,
        Range,
        Float,
        Texture,
        Int
    }
    
    public CrossShaderProperty(string both, Type type) {
        hdrp = both;
        urp = both;
        this.type = type;
    }
    
    public CrossShaderProperty(string hdrp, string urp, Type type) {
        this.hdrp = hdrp;
        this.urp = urp;
        this.type = type;
    }
}

public class HdrpMaskRemapper {
    public readonly string hdrpMask;
    public readonly string urpR, urpG, urpB, urpA;
    
    public HdrpMaskRemapper(string mask, string urpR, string urpG, string urpB, string urpA) {
        hdrpMask = mask;
        this.urpR = urpR;
        this.urpG = urpG;
        this.urpB = urpB;
        this.urpA = urpA;
    }
}
