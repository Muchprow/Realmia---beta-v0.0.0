using System;
using System.Collections.Generic;
using System.Reflection;
using Raylib_cs;

namespace Realmia
{
    public static class TextureManager
    {
        private static readonly Dictionary<BlockType, Texture2D> textures = new();
        private static readonly Dictionary<BlockType, object> models = new(); // object to hold Model (type from Raylib-cs)

        // Call once after Raylib.InitWindow
        public static void LoadDefaults()
        {
            Load(BlockType.Grass, "textures/grass.png");
            Load(BlockType.Dirt, "textures/dirt.png");
            Load(BlockType.Stone, "textures/stone.png");
        }

        public static void Load(BlockType type, string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return;
                var tex = Raylib.LoadTexture(path);
                textures[type] = tex;
                // try to create a simple cube model textured with this texture
                TryCreateTexturedCubeModel(type, tex);
            }
            catch (Exception)
            {
                // ignore; we'll fallback to color
            }
        }

        private static void TryCreateTexturedCubeModel(BlockType type, Texture2D tex)
        {
            try
            {
                var raylibType = typeof(Raylib);
                MethodInfo genMesh = null;
                // Prefer cube-generating GenMesh methods (GenMeshCube, GenMeshCubeTexture, etc.), fallback to any GenMesh
                foreach (var prefer in new[] { "GenMeshCube", "GenMeshCubeEx", "GenMeshCubeTexture", "GenMeshCubeTex", "GenMeshPoly" })
                {
                    genMesh = Array.Find(raylibType.GetMethods(BindingFlags.Public | BindingFlags.Static), m => m.Name.IndexOf(prefer, StringComparison.OrdinalIgnoreCase) >= 0 && m.ReturnType.Name.Contains("Mesh"));
                    if (genMesh != null) break;
                }
                if (genMesh == null)
                {
                    foreach (var m in raylibType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (m.Name.IndexOf("GenMesh", StringComparison.OrdinalIgnoreCase) >= 0 && m.ReturnType.Name.Contains("Mesh"))
                        {
                            genMesh = m; break;
                        }
                    }
                }

                Console.WriteLine("TextureManager: genMesh = " + (genMesh != null ? genMesh.Name : "<null>"));
                if (genMesh == null) return;

                // invoke genMesh with parameters (1,1,1) or suitable signature
                object mesh = null;
                var ps = genMesh.GetParameters();
                object[] args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var p = ps[i].ParameterType;
                    if (p == typeof(int)) args[i] = 1;
                    else if (p == typeof(float)) args[i] = 1f;
                    else args[i] = Activator.CreateInstance(p);
                }
                mesh = genMesh.Invoke(null, args);
                Console.WriteLine("TextureManager: genMesh invoked, mesh=" + (mesh != null ? mesh.GetType().FullName : "null"));

                // LoadModelFromMesh
                MethodInfo loadModelFromMesh = raylibType.GetMethod("LoadModelFromMesh", BindingFlags.Public | BindingFlags.Static);
                Console.WriteLine("TextureManager: loadModelFromMesh = " + (loadModelFromMesh != null ? loadModelFromMesh.Name : "<null>"));
                object model = null;

                if (loadModelFromMesh != null && mesh != null)
                {
                    model = loadModelFromMesh.Invoke(null, new object[] { mesh });
                    Console.WriteLine("TextureManager: model created=" + (model != null ? model.GetType().FullName : "null"));

                    // Extra diagnostic: inspect Materials array and material element structure
                    try
                    {
                        var modelTypeDiag = model.GetType();
                        var matsFieldDiag = modelTypeDiag.GetField("Materials", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                           ?? (MemberInfo)modelTypeDiag.GetProperty("Materials", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        Console.WriteLine("TextureManager: matsFieldDiag=" + (matsFieldDiag != null ? matsFieldDiag.Name : "<null>"));
                        object matsVal = null;
                        if (matsFieldDiag is FieldInfo mfd) matsVal = mfd.GetValue(model);
                        else if (matsFieldDiag is PropertyInfo mpd) matsVal = mpd.GetValue(model);
                        Console.WriteLine("TextureManager: matsVal type=" + (matsVal != null ? matsVal.GetType().FullName : "<null>"));
                        var matsArrDiag = matsVal as Array;
                        Console.WriteLine("TextureManager: matsArrDiag length=" + (matsArrDiag != null ? matsArrDiag.Length.ToString() : "<not array>"));
                        if (matsArrDiag != null && matsArrDiag.Length > 0)
                        {
                            var firstMat = matsArrDiag.GetValue(0);
                            var firstMatType = firstMat.GetType();
                            Console.WriteLine("TextureManager: firstMatType=" + firstMatType.FullName);
                            Console.WriteLine("TextureManager: firstMat fields: " + string.Join(", ", Array.ConvertAll(firstMatType.GetFields(BindingFlags.Public | BindingFlags.Instance), f => f.Name)));
                            Console.WriteLine("TextureManager: firstMat props: " + string.Join(", ", Array.ConvertAll(firstMatType.GetProperties(BindingFlags.Public | BindingFlags.Instance), p => p.Name)));
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("TextureManager: model diagnostic failed: " + e.Message);
                    }
                }
                else
                {
                    // maybe there's a method GenMeshCube that returns a Model directly? skip
                    return;
                }

                if (model == null) return;

                // Diagnostic: inspect model type members
                try
                {
                    var mt = model.GetType();
                    Console.WriteLine("TextureManager: model fields: " + string.Join(", ", Array.ConvertAll(mt.GetFields(BindingFlags.Public | BindingFlags.Instance), f => f.Name)));
                    Console.WriteLine("TextureManager: model props: " + string.Join(", ", Array.ConvertAll(mt.GetProperties(BindingFlags.Public | BindingFlags.Instance), p => p.Name)));
                }
                catch { }

                // Try to assign texture to model material using available SetMaterialTexture overloads
                bool applied = false;
                try
                {
                    var setMethods = Array.FindAll(raylibType.GetMethods(BindingFlags.Public | BindingFlags.Static), m => m.Name == "SetMaterialTexture");
                    Console.WriteLine("TextureManager: SetMaterialTexture overloads=" + setMethods.Length);
                    // prefer 4-param overload if present
                    foreach (var mset in setMethods)
                    {
                        var pars = mset.GetParameters();
                        if (pars.Length == 4)
                        {
                            // param types: (Model, int, MaterialMapType, Texture2D) or similar
                            var mapType = pars[2].ParameterType;
                            object mapVal = null;
                            foreach (var name in new[] { "MAP_ALBEDO", "MAP_DIFFUSE", "ALBEDO", "DIFFUSE", "TEXTURE", "MAP_ALBEDO_TEXTURE" })
                            {
                                try { mapVal = Enum.Parse(mapType, name, true); break; } catch { }
                            }
                            if (mapVal == null) mapVal = Enum.ToObject(mapType, 0);
                            try
                            {
                                mset.Invoke(null, new object[] { model, 0, mapVal, tex });
                                Console.WriteLine("TextureManager: SetMaterialTexture invoked (4 params) via " + mset);
                                applied = true; break;
                            }
                            catch (Exception e) { Console.WriteLine("TextureManager: SetMaterialTexture(4) failed: " + e.Message); }
                        }
                    }
                    if (!applied)
                    {
                        // try 3-param variants
                        foreach (var mset in setMethods)
                        {
                            var pars = mset.GetParameters();
                            if (pars.Length == 3)
                            {
                                try
                                {
                                    mset.Invoke(null, new object[] { model, 0, tex });
                                    Console.WriteLine("TextureManager: SetMaterialTexture invoked (3 params) via " + mset);
                                    applied = true; break;
                                }
                                catch (Exception e) { Console.WriteLine("TextureManager: SetMaterialTexture(3) failed: " + e.Message); }
                            }
                        }
                    }
                    if (!applied) Console.WriteLine("TextureManager: no SetMaterialTexture call applied");
                }
                catch (Exception e)
                {
                    Console.WriteLine("TextureManager: SetMaterialTexture dispatch failed: " + e.Message);
                }

                // fallback: try to set model.materials[0].maps[0].texture (single structured attempt) if SetMaterialTexture didn't apply
                if (!applied)
                {
                    try
                    {
                        Console.WriteLine("TextureManager: fallback to direct material field set (structured)");
                        var modelType = model.GetType();
                        var materialsMember = (MemberInfo)modelType.GetField("materials", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                              ?? (MemberInfo)modelType.GetProperty("materials", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        Console.WriteLine("TextureManager: materialsMember=" + (materialsMember != null ? materialsMember.Name : "<null>"));
                        if (materialsMember != null)
                        {
                            var mats = materialsMember is FieldInfo mfi2 ? mfi2.GetValue(model) : ((PropertyInfo)materialsMember).GetValue(model);
                            if (mats != null)
                            {
                                var matsArr = mats as Array;
                                Console.WriteLine("TextureManager: materials array length=" + (matsArr != null ? matsArr.Length.ToString() : "<not array>"));
                                if (matsArr != null)
                                {
                                    for (int mi = 0; mi < matsArr.Length; mi++)
                                    {
                                        var mat = matsArr.GetValue(mi);
                                        var matType = mat.GetType();
                                        Console.WriteLine($"TextureManager: material[{mi}] type={matType.FullName}");
                                        var mapsMember = (MemberInfo)matType.GetField("maps", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                                          ?? (MemberInfo)matType.GetProperty("maps", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                        Console.WriteLine("TextureManager: mapsMember=" + (mapsMember != null ? mapsMember.Name : "<null>"));
                                        if (mapsMember != null)
                                        {
                                            var maps = mapsMember is FieldInfo mapsFi ? mapsFi.GetValue(mat) : ((PropertyInfo)mapsMember).GetValue(mat);
                                            var mapsArr = maps as Array;
                                            Console.WriteLine("TextureManager: maps array length=" + (mapsArr != null ? mapsArr.Length.ToString() : "<not array>"));
                                            if (mapsArr != null)
                                            {
                                                for (int k = 0; k < mapsArr.Length; k++)
                                                {
                                                    var map = mapsArr.GetValue(k);
                                                    var mapType = map.GetType();
                                                    Console.WriteLine($"TextureManager: map[{k}] type={mapType.FullName}");
                                                    var texField = (MemberInfo)mapType.GetField("texture", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                                                                   ?? (MemberInfo)mapType.GetProperty("texture", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                                    Console.WriteLine("TextureManager: map.texture member=" + (texField != null ? texField.Name : "<null>"));
                                                    if (texField is FieldInfo tfi)
                                                    {
                                                        tfi.SetValue(map, tex);
                                                        Console.WriteLine("TextureManager: map.texture set via field");
                                                    }
                                                    else if (texField is PropertyInfo tpi && tpi.CanWrite)
                                                    {
                                                        tpi.SetValue(map, tex);
                                                        Console.WriteLine("TextureManager: map.texture set via property");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // store model even if texture assignment failed, so DrawModel can be used
                try
                {
                    models[type] = model;
                    Console.WriteLine("TextureManager: stored generated model for " + type);
                }
                catch { }
            }
            catch (Exception)
            {
                // ignore any reflection/errors; we'll render solid color fallback
            }
        }

        // Try get the loaded Texture2D for a BlockType
        public static bool TryGet(BlockType type, out Texture2D tex)
        {
            if (textures.TryGetValue(type, out var t))
            {
                tex = t;
                return true;
            }
            tex = default;
            return false;
        }

        // Try get the generated model object (Raylib Model) for a BlockType
        public static bool TryGetModel(BlockType type, out object model)
        {
            if (models.TryGetValue(type, out var m))
            {
                model = m;
                return true;
            }
            model = null;
            return false;
        }

        // Unload all loaded textures and models
        public static void UnloadAll()
        {
            // unload textures
            foreach (var kv in textures)
            {
                try { Raylib.UnloadTexture(kv.Value); } catch { }
            }
            textures.Clear();

            // unload models if Raylib exposes UnloadModel
            var raylibType = typeof(Raylib);
            var unloadModel = raylibType.GetMethod("UnloadModel", BindingFlags.Public | BindingFlags.Static);
            foreach (var kv in models)
            {
                try
                {
                    if (unloadModel != null && kv.Value != null)
                    {
                        try { unloadModel.Invoke(null, new object[] { kv.Value }); } catch { }
                    }
                }
                catch { }
            }
            models.Clear();
        }
    }

}
