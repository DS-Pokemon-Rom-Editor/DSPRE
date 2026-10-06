using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.Views.Controls
{
    /// <summary>
    /// A header's maps seen through a field camera, framed the way the game frames the player: what the
    /// camera table actually does, not a stored picture of it.
    /// </summary>
    public class CameraPreview : Grid
    {
        private readonly NsbmdGlControl _gl = new NsbmdGlControl { IsHitTestVisible = false };
        private readonly TextBlock _note = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Margin = new global::Avalonia.Thickness(8),
            IsVisible = false,
        };

        private readonly WeatherLayer _weather;
        private int _weatherShown = -1;
        private FieldWeather.Fog _fog;
        private int _sceneHeader = -1;
        private NsbmdRenderModel _scene;
        private FieldCameraEntry _camera;
        private (int x, int z)? _focus;
        private int _request;
        private string _builtFor, _weatherFor;

        public CameraPreview()
        {
            _note.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Editor.Subtle"));
            _weather = new WeatherLayer(_gl);
            Children.Add(_gl);
            AttachedToVisualTree += (_, _) => _weather.Running = true;
            DetachedFromVisualTree += (_, _) => _weather.Running = false;
            Children.Add(_note);
        }

        /// <summary>
        /// Shows <paramref name="headerId"/>'s maps through <paramref name="camera"/>, looking at
        /// <paramref name="focus"/> (a global tile, the way the fly table gives one) like the game looks at the player.
        /// </summary>
        public void Show(int headerId, FieldCameraEntry camera, (int x, int z)? focus = null)
        {
            _camera = camera;
            _focus = focus;
            if (headerId == _sceneHeader && _builtFor == RomInfo.workDir) { Frame(); return; }

            _sceneHeader = headerId;
            _builtFor = RomInfo.workDir;
            int request = ++_request;
            _ = BuildAsync(headerId, request);
        }

        /// <summary>Plays the field weather a header value gives, over the view.</summary>
        public void ShowWeather(int headerWeather)
        {
            if (headerWeather == _weatherShown && _weatherFor == RomInfo.workDir) return;
            _weatherShown = headerWeather;
            _weatherFor = RomInfo.workDir;
            _weather.Show(headerWeather);
            _fog = FieldWeather.For(RomInfo.gameFamily, headerWeather).FogSettings;
            Frame();
        }

        /// <summary>Forgets the built maps, so the next <see cref="Show"/> reads them again.</summary>
        public void Reload() => _sceneHeader = -1;

        private async Task BuildAsync(int headerId, int request)
        {
            NsbmdRenderModel scene = null;
            string note = null;
            SceneBuildingAnimator buildings = null;
            try
            {
                (scene, note, buildings) = await Task.Run(() => BuildScene(headerId));
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Camera preview of header {headerId} failed: {ex.Message}");
                note = "No preview for this header.";
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (request != _request) return;   // a later header was asked for meanwhile
                _scene = scene;
                _note.Text = note;
                _note.IsVisible = scene == null;
                _gl.IsVisible = scene != null;
                if (scene != null) _gl.SetModel(scene);
                _gl.BuildingAnimator = buildings;
                Frame();
            });
        }

        private static (NsbmdRenderModel scene, string note, SceneBuildingAnimator buildings) BuildScene(int headerId)
        {
            if (headerId < 0) return (null, null, null);
            MatrixSceneBuilder.EnsureUnpacked();
            MapHeader header = MapHeader.GetMapHeader((ushort)headerId);
            if (header == null) return (null, "No preview for this header.", null);
            GameMatrix matrix = new GameMatrix(header.matrixID);

            HashSet<(int x, int y)> cells = matrix.CellsOfHeader(headerId);
            if (cells != null && FieldCatchAllHeader.IsCatchAll(cells.Count))
                return (null, "This header has no place of its own to show.", null);
            // Without a headers section the whole matrix is the place, unless it is world-sized.
            if (cells == null && matrix.width * matrix.height > 256)
                return (null, "No preview for this header.", null);

            NsbmdRenderModel scene = MatrixSceneBuilder.Build(matrix, header.areaDataID, RomInfo.gameFamily, areaForMap: null, includeCells: cells);
            if (scene == null) return (null, "No maps to show.", null);
            // HGSS interior areas take their buildings from the interior set, and so their animations too.
            bool indoor = false;
            try { indoor = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS && new AreaData(header.areaDataID).areaType == AreaData.TYPE_INDOOR; } catch { }
            SceneBuildingAnimator buildings = null;
            try { buildings = new SceneBuildingAnimator(scene, indoor, FieldTimeOfDay.Now); }
            catch (Exception ex) { AppLogger.Warn("Building animations for the field view: " + ex.Message); }
            return (scene, null, buildings);
        }

        // The focus tile's ground in the scene's own space, placed the way the event editor places a marker.
        private static (float x, float y, float z) Target(NsbmdRenderModel scene, (int x, int z)? focus)
        {
            if (focus is not (int gx, int gz) || scene.CellStrideX == 0)
            {
                float ground = scene.HasMapBounds ? scene.MapMinY : scene.RawMinY;
                return (0f, (ground - scene.Cy) * scene.Scale, 0f);
            }

            int cellX = gx / MapFile.mapSize, cellZ = gz / MapFile.mapSize;
            float inX = gx % MapFile.mapSize + 0.5f, inZ = gz % MapFile.mapSize + 0.5f;
            float rawX, rawZ;
            if (scene.TryCellPlacement(cellX, cellZ, out NsbmdRenderModel.CellPlacement p))
            {
                rawX = p.OriginX + inX / MapFile.mapSize * p.Width;
                rawZ = p.OriginZ + inZ / MapFile.mapSize * p.Height;
            }
            else
            {
                rawX = scene.CellBaseX + (cellX + inX / MapFile.mapSize) * scene.CellStrideX;
                rawZ = scene.CellBaseZ + (cellZ + inZ / MapFile.mapSize) * scene.CellStrideZ;
            }
            float rawY = scene.SurfaceY(rawX, rawZ);
            return ((rawX - scene.Cx) * scene.Scale, (rawY - scene.Cy) * scene.Scale, (rawZ - scene.Cz) * scene.Scale);
        }

        // ── HGSS darkness ────────────────────────────────────────────────────────

        // The light model draws its front face only and without fog.
        private static NsbmdGlControl.LightSpot _spotShape;
        private static string _spotShapeFor;

        private void ShowSpot(NsbmdRenderModel scene, (float x, float y, float z)? foot)
        {
            FieldWeather.Spec spec = FieldWeather.For(RomInfo.gameFamily, Math.Max(0, _weatherShown));
            bool dark = spec.Kind == FieldWeather.Kind.HgFlash && _weatherShown >= 0;
            _gl.ClearColour = dark ? (0f, 0f, 0f) : (0.12f, 0.12f, 0.14f);
            NsbmdGlControl.LightSpot shape = dark ? SpotShape() : null;
            if (shape == null || foot is not (float fx, float fy, float fz)) { _gl.SetLightSpot(null); return; }

            // A third of the way from the player to the camera, scaled up four times once Flash is used.
            (float ex, float ey, float ez) = _gl.EyePosition();
            float scale = spec.SpotScale * scene.Scale;
            _gl.SetLightSpot(new NsbmdGlControl.LightSpot
            {
                Center = new[] { fx + (ex - fx) / 3f, fy + (ey - fy) / 3f, fz + (ez - fz) / 3f },
                ScaleX = scale, ScaleY = scale,
                Triangles = shape.Triangles, Rgba = shape.Rgba, Width = shape.Width, Height = shape.Height,
            });
        }

        private static NsbmdGlControl.LightSpot SpotShape()
        {
            if (_spotShapeFor == RomInfo.workDir) return _spotShape;
            _spotShapeFor = RomInfo.workDir;
            _spotShape = null;
            try
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fieldEffectModels });
                string path = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.fieldEffectModels].unpackedDir, FieldWeather.HgFlashSpotModel.ToString("D4"));
                using FileStream file = File.OpenRead(path);
                NSBMD nsbmd = global::LibNDSFormats.NSBMD.NSBMDLoader.LoadNSBMD(file);
                nsbmd.MatchTextures();
                NSBMDModel model = nsbmd.models[0];
                NsbmdTextureData tex = NsbmdTextureDecoder.Decode(model.Materials[0]);
                NsbmdRenderModel built = NsbmdGeometry.BuildModel(model);
                // Every quad of the grid carries its own texture coordinates, so the triangles go as they are.
                List<float> corners = new List<float>();
                foreach (NsbmdMeshPart part in built.Parts)
                    for (int i = 0; i + 7 < part.Vertices.Length; i += 8)
                        corners.AddRange(new[] { part.Vertices[i], part.Vertices[i + 1], part.Vertices[i + 3], part.Vertices[i + 4] });
                if (tex == null || corners.Count < 12) return null;
                _spotShape = new NsbmdGlControl.LightSpot
                {
                    Triangles = corners.ToArray(), Rgba = tex.Rgba, Width = tex.Width, Height = tex.Height,
                };
            }
            catch (Exception ex) { AppLogger.Warn("The Flash light model could not be read: " + ex.Message); }
            return _spotShape;
        }

        // The hero where Fly sets you down, facing the camera, at the size an overworld sprite stands.
        private void ShowPlayer((float x, float y, float z)? foot, float unit)
        {
            OverworldSprites.SpritePixels pix = null;
            if (foot != null)
            {
                const ushort hero = 0;
                int picture = DSPRE.ROMFiles.FieldSpriteAnimation.PictureFor(OverworldSprites.FrameCount(hero),
                    (int)DSPRE.ROMFiles.MoveFacing.Down, default);
                pix = OverworldSprites.Get(hero, (ushort)DSPRE.ROMFiles.MoveFacing.Down, picture);
            }
            if (pix == null || pix.Width <= 0 || foot is not (float fx, float fy, float fz)) { _gl.SetSprites(null); return; }

            float halfW = unit * pix.Width / (OverworldSprites.PixelsPerTile * 2f);
            float halfH = unit * pix.Height / (OverworldSprites.PixelsPerTile * 2f);
            _gl.SetSprites(new[]
            {
                new NsbmdGlControl.SpriteInstance
                {
                    Cx = fx, Cy = fy + halfH, Cz = fz, HalfW = halfW, HalfH = halfH,
                    Rgba = pix.Rgba, Width = pix.Width, Height = pix.Height,
                },
            });
        }

        private void Frame()
        {
            NsbmdRenderModel scene = _scene;
            FieldCameraEntry cam = _camera;
            if (scene == null || cam == null) return;

            // One tile in the scene's own units, the same conversion the animated preview uses.
            float tile = scene.CellStrideX / MapFile.mapSize;
            float unit = tile * scene.Scale;
            (float x, float y, float z) = Target(scene, _focus);

            _gl.LookAt(x + cam.ShiftXInTiles * unit, y + cam.ShiftYInTiles * unit, z + cam.ShiftZInTiles * unit);
            ShowPlayer(_focus != null ? (x, y, z) : null, unit);
            _gl.Distance = cam.DistanceForScene(tile) * scene.Scale;
            _gl.Orthographic = cam.Orthographic;
            _gl.VerticalFieldOfViewDegrees = cam.FieldOfViewDegrees;
            _gl.Roll = cam.RollDegrees;
            // The game turns the camera's offset by +yaw; the orbit view turns the world, so the sign flips.
            _gl.SetOrientation(-cam.YawDegrees, cam.PitchDegrees);

            // Fog works in the game's own depth, so the view's distances go back into game units.
            _gl.SetFog(unit > 0 ? _fog : null, cam.NearClip, cam.FarClip, unit > 0 ? FieldCameraEntry.GameUnitsPerTile / unit : 0);
            ShowSpot(scene, _focus != null ? (x, y, z) : null);
        }
    }
}
