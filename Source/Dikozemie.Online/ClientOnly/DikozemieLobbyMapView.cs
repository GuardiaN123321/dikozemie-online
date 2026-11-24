using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    public abstract class DikozemieLobbyMapTextureProviderBase : TextureProvider
    {
        private readonly string _fileName;
        private readonly string _textureName;
        private TaleWorlds.Engine.Texture _engineTexture;
        private TaleWorlds.TwoDimension.Texture _texture;
        private bool _loadAttempted;

        protected DikozemieLobbyMapTextureProviderBase(
            string fileName,
            string textureName)
        {
            _fileName = fileName;
            _textureName = textureName;
        }

        protected override TaleWorlds.TwoDimension.Texture OnGetTextureForRender(
            TwoDimensionContext twoDimensionContext,
            string name)
        {
            EnsureLoaded();
            if (_engineTexture == null)
            {
                return null;
            }

            if (_texture == null)
            {
                _texture = new TaleWorlds.TwoDimension.Texture(
                    new EngineTexture(_engineTexture));
            }
            return _texture;
        }

        public override void Clear(bool clearNextFrame)
        {
            _texture = null;
            if (_engineTexture != null && !_engineTexture.IsReleased)
            {
                if (clearNextFrame)
                {
                    _engineTexture.ReleaseAfterNumberOfFrames(2);
                }
                else
                {
                    _engineTexture.Release();
                }
            }
            _engineTexture = null;
            _loadAttempted = false;
            base.Clear(clearNextFrame);
        }

        private void EnsureLoaded()
        {
            if (_loadAttempted)
            {
                return;
            }

            _loadAttempted = true;
            string path = System.IO.Path.Combine(
                ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                "GUI",
                "Textures",
                _fileName);
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine("DikozemieOnline: lobby map texture is missing: " + path);
                    return;
                }

                _engineTexture = TaleWorlds.Engine.Texture.CreateFromMemory(
                    File.ReadAllBytes(path));
                if (_engineTexture != null)
                {
                    _engineTexture.Name = _textureName;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: lobby map texture failed: " + error);
                _engineTexture = null;
            }
        }
    }

    public sealed class DikozemieLobbyMapTextureProvider
        : DikozemieLobbyMapTextureProviderBase
    {
        public DikozemieLobbyMapTextureProvider()
            : base(
                "calradia-multiplayer-map.jpg",
                "dikozemie_multiplayer_calradia_map_overview")
        {
        }
    }

    public abstract class DikozemieMapIconProviderBase : TextureProvider
    {
        private static readonly Dictionary<string, TaleWorlds.TwoDimension.Texture> Loaded =
            new Dictionary<string, TaleWorlds.TwoDimension.Texture>();
        private readonly string _fileName;

        protected DikozemieMapIconProviderBase(string fileName)
        {
            _fileName = fileName;
        }

        protected override TaleWorlds.TwoDimension.Texture OnGetTextureForRender(
            TwoDimensionContext twoDimensionContext,
            string name)
        {
            if (Loaded.TryGetValue(_fileName, out TaleWorlds.TwoDimension.Texture texture))
            {
                return texture;
            }
            try
            {
                string path = System.IO.Path.Combine(
                    ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                    "GUI", "Textures", _fileName);
                TaleWorlds.Engine.Texture engine =
                    TaleWorlds.Engine.Texture.CreateFromMemory(File.ReadAllBytes(path));
                if (engine != null)
                {
                    // Without its own name the engine hands back the first image.
                    engine.Name = "dikozemie_" + _fileName;
                }
                texture = engine == null
                    ? null
                    : new TaleWorlds.TwoDimension.Texture(new EngineTexture(engine));
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: map icon failed: " + error.Message);
                texture = null;
            }
            Loaded[_fileName] = texture;
            return texture;
        }

        public override void Clear(bool clearNextFrame)
        {
            // The texture stays: all widgets share it for the whole session.
            base.Clear(clearNextFrame);
        }
    }

    public sealed class DikozemieMapIconVillageTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconVillageTextureProvider() : base("map-icon-village.png") { }
    }

    public sealed class DikozemieMapIconHeroTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconHeroTextureProvider() : base("map-icon-hero.png") { }
    }

    public sealed class DikozemieMapIconBoatTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconBoatTextureProvider() : base("map-icon-boat.png") { }
    }

    public sealed class DikozemieMapIconBoatLeftTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconBoatLeftTextureProvider() : base("map-icon-boat-left.png") { }
    }

    public sealed class DikozemieMapVisionMaskTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapVisionMaskTextureProvider() : base("map-vision-mask.png") { }
    }

    public sealed partial class DikozemieLobbyMapWidget : Widget
    {
        private const float SourceWidth = 6740f;
        private const float SourceHeight = 4768f;
        private const float TileSize = 512f;
        private const int TileColumns = 14;
        private const int TileRows = 10;
        private const float SidePanelWidth = 430f;
        private const float OuterMargin = 34f;
        private const float DetailZoomThreshold = 2f;
        private const float TownLabelZoom = 1f;
        private const float CastleLabelZoom = 3f;
        private const float VillageLabelZoom = 6f;
        private const float RouteSpacing = 17f;
        private const float BoatFigureSize = 74f;
        private const float BoatTurnDistance = 30f;
        private const int RouteDotCount = 180;
        private const float VisionBase = 260f;
        private const float VisionPerScouting = 2f;
        private const float VisionMaskHole = 180f / 512f;
        private const float FollowPause = 4f;

        private sealed class MapTile
        {
            public TextureWidget Widget;
            public string ProviderName;
            public int Column;
            public int Row;
            public float PixelWidth;
            public float PixelHeight;
            // 0.5.133: where the tile lies on the atlas, and whether it is one
            // of the twice-as-fine tiles shown close up.
            public float AtlasX;
            public float AtlasY;
            public bool Fine;
        }

        // The fine tiles (LobbyMap/5, twice the atlas resolution, 256 atlas
        // pixels each) take over once an atlas pixel is drawn this large.
        private const float FineTileScale = 1.1f;
        private const int FineTileColumns = 27;
        private const int FineTileRows = 19;
        // On the 0.5.133 map every town, castle and village is painted
        // (Tools/render_world_map.py, settlement_layer): widths in atlas
        // pixels, the base of the drawing a little below the point.
        private bool _paintedMap;
        private const float PaintedTown = 58f;
        private const float PaintedCastle = 42f;
        private const float PaintedVillage = 30f;

        private sealed class MapMarker
        {
            public DikozemieLobbyMapNode Node;
            public Widget Halo;
            public Widget Dot;
            public TextureWidget Icon;
            public TextWidget Label;
            public float LabelDx = 11f;
            public float LabelDy = -15f;
            public bool LabelShown;
            public int Rank;
        }

        private readonly List<MapMarker> _markers = new List<MapMarker>();
        private readonly List<MapMarker> _labelOrder = new List<MapMarker>();
        private readonly List<float> _labelBoxes = new List<float>();
        private float _labelsMapWidth = -1f;
        private DikozemieLobbyMapNode _labelsSelected;
        private float _labelsFigureX = float.MinValue;
        private float _labelsFigureY = float.MinValue;
        private bool _labelsBoat;
        private readonly List<MapTile> _detailTiles = new List<MapTile>();
        private readonly List<Widget> _routeHalos = new List<Widget>();
        private readonly List<Widget> _routeSegments = new List<Widget>();
        private TextureWidget _map;
        private Widget _mapShadow;
        private Widget _sidePanel;
        private Widget _sideColumn;
        private TextWidget _title;
        private TextWidget _description;
        private TextWidget _zoomText;
        private TextWidget _legend;
        private ButtonWidget _arenaButton;
        private ButtonWidget _navalButton;
        private ButtonWidget _goButton;
        private TextWidget _goText;
        private ButtonWidget _boatButton;
        private TextWidget _boatText;
        private Widget _routeModePlate;
        private TextWidget _routeModeText;
        private Widget _visionRing;
        private TextureWidget _visionMask;
        private readonly Widget[] _visionSheets = new Widget[4];
        private TextureWidget _heroFigure;
        private TextureWidget _boatFigure;
        private TextureWidget _boatFigureLeft;
        private bool _boatFacesLeft;
        private float _boatBob;
        private float _boatTurnX;
        private float _panelRefresh;
        private TextWidget _hereLabel;
        private DikozemieLobbyMapNode _homeNode;
        private DikozemieLobbyMapNode _selectedNode;
        private DikozemieTravelSave _travel;
        private DikozemieTravelRoute _route;
        private bool _hasSelectedPoint;
        private float _selectedX;
        private float _selectedY;
        private bool _travelling;
        private int _routeIndex;
        private float _untilFollow;
        private float _saveCountdown;
        private string _travelNote = string.Empty;
        private float _zoom = 1f;
        private float _panX;
        private float _panY;
        private float _detailLoadCooldown;
        private bool _built;

        private const float MinZoom = 1f;
        private const float MaxZoom = 16f;

        public DikozemieLobbyMapWidget(UIContext context)
            : base(context)
        {
            ClipContents = true;
        }

        protected override void OnConnectedToRoot()
        {
            base.OnConnectedToRoot();
            BuildContent();
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            _detailLoadCooldown = Math.Max(0f, _detailLoadCooldown - dt);
            if (!_built)
            {
                BuildContent();
            }
            TickClanRequests();
            TickTownRequests();
            TickRoadReturn();
            TickTestRoadCard();
            TickTestTownWalk();
            if (IsVisible && DikozemieRoads.PeddlerOpen)
            {
                DikozemieRoads.OpenPeddler(false);
            }
            TickTravel(dt);
            TickWorld(dt);
            TickTownHover();
            LayoutContent();
            TickTradeSheet();
            TickChatSheet(dt);
        }

        // Mouse positions come in screen pixels; everything here is in the
        // widget's own units, which differ on other screen sizes.
        private float ViewWidth => Size.X * _inverseScaleToUse;
        private float ViewHeight => Size.Y * _inverseScaleToUse;

        private Vec2 ToLocal(Vec2 cursor) => new Vec2(
            (cursor.X - GlobalPosition.X) * _inverseScaleToUse,
            (cursor.Y - GlobalPosition.Y) * _inverseScaleToUse);

        public void Zoom(float wheelDelta, Vec2 cursor)
        {
            if (LayerOpen || _roadOpen || Math.Abs(wheelDelta) < 0.01f || Size.X <= 0f || Size.Y <= 0f)
            {
                return;
            }
            cursor = ToLocal(cursor);
            GetViewport(out float viewportX, out float viewportY,
                out float viewportWidth, out float viewportHeight);
            if (cursor.X < viewportX || cursor.X > viewportX + viewportWidth ||
                cursor.Y < viewportY || cursor.Y > viewportY + viewportHeight)
            {
                return;
            }
            GetMapRectangle(out float oldX, out float oldY,
                out float oldWidth, out float oldHeight);
            float u = oldWidth <= 0f ? 0.5f : (cursor.X - oldX) / oldWidth;
            float v = oldHeight <= 0f ? 0.5f : (cursor.Y - oldY) / oldHeight;
            float factor = wheelDelta > 0f ? 1.18f : 1f / 1.18f;
            _zoom = Math.Max(MinZoom, Math.Min(MaxZoom, _zoom * factor));
            GetFittedMapSize(viewportWidth, viewportHeight,
                out float fittedWidth, out float fittedHeight);
            float newWidth = fittedWidth * _zoom;
            float newHeight = fittedHeight * _zoom;
            float baseX = viewportX + (viewportWidth - newWidth) * 0.5f;
            float baseY = viewportY + (viewportHeight - newHeight) * 0.5f;
            _panX = cursor.X - baseX - u * newWidth;
            _panY = cursor.Y - baseY - v * newHeight;
            ClampPan(newWidth, newHeight, viewportWidth, viewportHeight);
            LayoutContent();
        }

        public void Pan(float deltaX, float deltaY)
        {
            if (LayerOpen || _roadOpen || _zoom <= MinZoom)
            {
                return;
            }
            deltaX *= _inverseScaleToUse;
            deltaY *= _inverseScaleToUse;
            GetViewport(out _, out _, out float viewportWidth, out float viewportHeight);
            GetFittedMapSize(viewportWidth, viewportHeight,
                out float fittedWidth, out float fittedHeight);
            float mapWidth = fittedWidth * _zoom;
            float mapHeight = fittedHeight * _zoom;
            _panX += deltaX;
            _panY += deltaY;
            if (Math.Abs(deltaX) + Math.Abs(deltaY) > 0.01f)
            {
                _untilFollow = FollowPause;
            }
            ClampPan(mapWidth, mapHeight, viewportWidth, viewportHeight);
            LayoutContent();
        }

        public void ResetView()
        {
            _zoom = MinZoom;
            _panX = 0f;
            _panY = 0f;
            LayoutContent();
        }

        public void SelectMapPoint(Vec2 cursor)
        {
            if (LayerOpen || _roadOpen)
            {
                return;
            }
            cursor = ToLocal(cursor);
            GetViewport(out float viewportX, out float viewportY,
                out float viewportWidth, out float viewportHeight);
            if (cursor.X < viewportX || cursor.X > viewportX + viewportWidth ||
                cursor.Y < viewportY || cursor.Y > viewportY + viewportHeight)
            {
                return;
            }

            GetMapRectangle(out float mapX, out float mapY,
                out float mapWidth, out float mapHeight);
            if (cursor.X < mapX || cursor.X > mapX + mapWidth ||
                cursor.Y < mapY || cursor.Y > mapY + mapHeight)
            {
                return;
            }

            // Other heroes, caravans and ships of the shared world first.
            if (TryPickWorld(cursor))
            {
                return;
            }
            _worldPick = null;
            _worldNote = string.Empty;

            // The lair of a wanted man is picked before any settlement.
            DikozemieTravelSave save = DikozemieTravelState.Current;
            if (save != null && DikozemieRoads.HasBounty)
            {
                float lairX = mapX + save.BountyX / SourceWidth * mapWidth;
                float lairY = mapY + save.BountyY / SourceHeight * mapHeight;
                float ldx = cursor.X - lairX;
                float ldy = cursor.Y - lairY;
                if (ldx * ldx + ldy * ldy <= 18f * 18f)
                {
                    SelectPoint(save.BountyX, save.BountyY);
                    _travelNote = "Логово: " + save.BountyName + ". Подойдите — и решите, нападать ли.";
                    RefreshSidePanel();
                    return;
                }
            }

            DikozemieLobbyMapNode nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            foreach (MapMarker marker in _markers)
            {
                DikozemieLobbyMapNode node = marker.Node;
                if (node?.Position == null || !IsNodeVisible(node))
                {
                    continue;
                }
                float x = mapX + ((node.Position.X / SourceWidth) * mapWidth);
                float y = mapY + ((node.Position.Y / SourceHeight) * mapHeight);
                float dx = cursor.X - x;
                float dy = cursor.Y - y;
                float distanceSquared = (dx * dx) + (dy * dy);
                float radius = IsTown(node) ? 28f : IsCastle(node) ? 22f : 16f;
                if (distanceSquared <= radius * radius &&
                    distanceSquared < nearestDistanceSquared)
                {
                    nearest = node;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            if (nearest != null)
            {
                SelectNode(nearest);
                return;
            }
            SelectPoint(
                ((cursor.X - mapX) / mapWidth) * SourceWidth,
                ((cursor.Y - mapY) / mapHeight) * SourceHeight);
        }

        private void BuildContent()
        {
            if (_built || Context == null)
            {
                return;
            }

            _built = true;
            AddChild(CreateSolid("Backdrop", new Color(0.025f, 0.022f, 0.018f, 1f)));

            // The native matchmaking page remains behind this layer. Consume
            // pointer events below the top navigation bar so its hidden cards
            // cannot display hints through the world map.
            var eventBlocker = new ButtonWidget(Context)
            {
                Id = "MapEventBlocker",
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                DoNotPassEventsToChildren = true
            };
            AddChild(eventBlocker);

            _mapShadow = CreateSolid(
                "MapShadow",
                new Color(0.05f, 0.025f, 0.01f, 0.92f));
            _mapShadow.WidthSizePolicy = SizePolicy.Fixed;
            _mapShadow.HeightSizePolicy = SizePolicy.Fixed;
            AddChild(_mapShadow);

            _map = new TextureWidget(Context)
            {
                Id = "CalradiaMap",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextureProviderName = "DikozemieLobbyMapTextureProvider",
                DoNotAcceptEvents = true
            };
            AddChild(_map);

            // A single 6740x4768 GPU texture exceeds the safe texture size on
            // some Bannerlord installations. Reuse the real 512px pyramid as
            // independently streamed widgets instead. Only visible tiles are
            // materialized, and the overview remains underneath while loading.
            for (int row = 0; row < TileRows; row++)
            {
                for (int column = 0; column < TileColumns; column++)
                {
                    float pixelX = column * TileSize;
                    float pixelY = row * TileSize;
                    // No provider until UpdateTiles gives it its own. A new
                    // TextureWidget has the engine's ResourceTextureProvider,
                    // which loads a texture by a null name and kills the render
                    // thread when the first frame already shows the tile (found
                    // 2026-11-20: map opened at 300 % on a resumed passage).
                    var tileWidget = new TextureWidget(Context)
                    {
                        Id = "CalradiaMapTile" + column + "x" + row,
                        WidthSizePolicy = SizePolicy.Fixed,
                        HeightSizePolicy = SizePolicy.Fixed,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        IsVisible = false,
                        DoNotAcceptEvents = true,
                        TextureProviderName = string.Empty
                    };
                    AddChild(tileWidget);
                    _detailTiles.Add(new MapTile
                    {
                        Widget = tileWidget,
                        ProviderName = "DikozemieLobbyMapTileC" +
                            column.ToString("00") + "R" + row.ToString("00") +
                            "TextureProvider",
                        Column = column,
                        Row = row,
                        PixelWidth = Math.Min(TileSize, SourceWidth - pixelX),
                        PixelHeight = Math.Min(TileSize, SourceHeight - pixelY),
                        AtlasX = pixelX,
                        AtlasY = pixelY
                    });
                }
            }
            // The map drawn for 0.5.133 has a second, finer level for close up
            // (Tools/render_world_map.py). Above the coarse tiles, so a fine
            // tile still loading shows the coarse one under it.
            string fineFolder = System.IO.Path.Combine(
                ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId), "GUI", "Textures", "LobbyMap", "5");
            _paintedMap = File.Exists(System.IO.Path.Combine(fineFolder, "0-0.jpg"));
            if (_paintedMap)
            {
                const float fineSpan = TileSize / 2f;
                for (int row = 0; row < FineTileRows; row++)
                {
                    for (int column = 0; column < FineTileColumns; column++)
                    {
                        float atlasX = column * fineSpan;
                        float atlasY = row * fineSpan;
                        var fineWidget = new TextureWidget(Context)
                        {
                            Id = "CalradiaMapFineTile" + column + "x" + row,
                            WidthSizePolicy = SizePolicy.Fixed,
                            HeightSizePolicy = SizePolicy.Fixed,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Top,
                            IsVisible = false,
                            DoNotAcceptEvents = true,
                            TextureProviderName = string.Empty
                        };
                        AddChild(fineWidget);
                        _detailTiles.Add(new MapTile
                        {
                            Widget = fineWidget,
                            ProviderName = "DikozemieLobbyMapTile5C" +
                                column.ToString("00") + "R" + row.ToString("00") +
                                "TextureProvider",
                            Column = column,
                            Row = row,
                            PixelWidth = Math.Min(fineSpan, SourceWidth - atlasX),
                            PixelHeight = Math.Min(fineSpan, SourceHeight - atlasY),
                            AtlasX = atlasX,
                            AtlasY = atlasY,
                            Fine = true
                        });
                    }
                }
            }

            BuildNightShade();
            _visionMask = new TextureWidget(Context)
            {
                Id = "TravelVisionMask",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextureProviderName = "DikozemieMapVisionMaskTextureProvider",
                DoNotAcceptEvents = true,
                IsVisible = false
            };
            AddChild(_visionMask);
            for (int index = 0; index < _visionSheets.Length; index++)
            {
                _visionSheets[index] = CreateSolid(
                    "TravelVisionSheet" + index,
                    new Color(6f / 255f, 4f / 255f, 3f / 255f, 190f / 255f));
                _visionSheets[index].WidthSizePolicy = SizePolicy.Fixed;
                _visionSheets[index].HeightSizePolicy = SizePolicy.Fixed;
                _visionSheets[index].IsVisible = false;
                AddChild(_visionSheets[index]);
            }

            for (int index = 0; index < RouteDotCount; index++)
            {
                Widget halo = CreateRouteDot(
                    "LobbyRouteHalo" + index,
                    10f,
                    new Color(0.08f, 0.035f, 0.012f, 0.72f));
                AddChild(halo);
                _routeHalos.Add(halo);

                Widget segment = CreateRouteDot(
                    "LobbyRouteDot" + index,
                    6f,
                    new Color(0.98f, 0.76f, 0.22f, 1f));
                AddChild(segment);
                _routeSegments.Add(segment);
            }

            _routeModePlate = CreateSolid(
                "LobbyRouteModePlate",
                new Color(0.10f, 0.055f, 0.02f, 0.94f));
            _routeModePlate.WidthSizePolicy = SizePolicy.Fixed;
            _routeModePlate.HeightSizePolicy = SizePolicy.Fixed;
            _routeModePlate.SuggestedWidth = 250f;
            _routeModePlate.SuggestedHeight = 34f;
            _routeModePlate.IsVisible = false;
            AddChild(_routeModePlate);
            _routeModeText = CreateText(
                "LobbyRouteModeText",
                "СУХОПУТНЫЙ ПУТЬ",
                16,
                new Color(0.98f, 0.83f, 0.42f, 1f));
            _routeModeText.WidthSizePolicy = SizePolicy.StretchToParent;
            _routeModeText.HeightSizePolicy = SizePolicy.StretchToParent;
            _routeModePlate.AddChild(_routeModeText);

            foreach (DikozemieLobbyMapNode node in DikozemieLobbyMapCatalog.Nodes)
            {
                _markers.Add(CreateSettlementMarker(node));
            }
            _homeNode = DikozemieLobbyMapCatalog.FindHomeNode(
                DikozemieHeroProfile.CultureId);
            BuildLairMarker();
            BuildWorldLayer();

            _visionRing = new Widget(Context)
            {
                Id = "TravelVisionRing",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteCircle"),
                Color = new Color(0.98f, 0.86f, 0.52f, 0.13f),
                DoNotAcceptEvents = true
            };
            AddChild(_visionRing);
            _heroFigure = CreateFigure("TravelHeroFigure", "DikozemieMapIconHeroTextureProvider");
            _boatFigure = CreateFigure("TravelBoatFigure", "DikozemieMapIconBoatTextureProvider");
            _boatFigureLeft = CreateFigure("TravelBoatFigureLeft", "DikozemieMapIconBoatLeftTextureProvider");
            _boatFigure.SuggestedWidth = BoatFigureSize;
            _boatFigure.SuggestedHeight = BoatFigureSize;
            _boatFigureLeft.SuggestedWidth = BoatFigureSize;
            _boatFigureLeft.SuggestedHeight = BoatFigureSize;
            _hereLabel = CreateText("TravelHereLabel", "Вы здесь", 18, new Color(0.16f, 0.09f, 0.04f, 1f));
            _hereLabel.WidthSizePolicy = SizePolicy.Fixed;
            _hereLabel.HeightSizePolicy = SizePolicy.Fixed;
            _hereLabel.SuggestedWidth = 160f;
            _hereLabel.SuggestedHeight = 28f;
            _hereLabel.Brush.DefaultStyle.TextGlowColor = new Color(0.98f, 0.92f, 0.78f, 1f);
            _hereLabel.Brush.DefaultStyle.TextGlowRadius = 0.2f;
            _hereLabel.Brush.DefaultStyle.TextBlur = 0.6f;
            AddChild(_hereLabel);

            _sideColumn = CreateSolid(
                "MapSideColumn",
                new Color(0.025f, 0.022f, 0.018f, 1f));
            _sideColumn.WidthSizePolicy = SizePolicy.Fixed;
            _sideColumn.HeightSizePolicy = SizePolicy.Fixed;
            AddChild(_sideColumn);

            _sidePanel = CreateSolid(
                "MapInformationPanel",
                new Color(0.065f, 0.047f, 0.028f, 1f));
            _sidePanel.WidthSizePolicy = SizePolicy.Fixed;
            _sidePanel.HeightSizePolicy = SizePolicy.Fixed;
            AddChild(_sidePanel);

            _title = CreateText(
                "MapTitle",
                "КАРТА ДИКОЗЕМЬЯ",
                34,
                new Color(0.94f, 0.73f, 0.31f, 1f));
            _title.WidthSizePolicy = SizePolicy.StretchToParent;
            _title.HeightSizePolicy = SizePolicy.Fixed;
            _title.SuggestedHeight = 72f;
            _title.MarginLeft = 24f;
            _title.MarginRight = 24f;
            _title.MarginTop = 28f;
            _sidePanel.AddChild(_title);

            Widget divider = CreateSolid(
                "TitleDivider",
                new Color(0.72f, 0.47f, 0.17f, 0.85f));
            divider.WidthSizePolicy = SizePolicy.StretchToParent;
            divider.HeightSizePolicy = SizePolicy.Fixed;
            divider.SuggestedHeight = 2f;
            divider.MarginLeft = 28f;
            divider.MarginRight = 28f;
            divider.PositionYOffset = 104f;
            _sidePanel.AddChild(divider);

            _description = CreateText(
                "MapDescription",
                string.Empty,
                21,
                new Color(0.88f, 0.82f, 0.70f, 1f));
            _description.WidthSizePolicy = SizePolicy.StretchToParent;
            _description.HeightSizePolicy = SizePolicy.Fixed;
            _description.SuggestedHeight = 300f;
            _description.MarginLeft = 30f;
            _description.MarginRight = 30f;
            _description.PositionYOffset = 140f;
            _description.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            _description.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
            _sidePanel.AddChild(_description);

            _zoomText = CreateText(
                "MapZoom",
                "МАСШТАБ 100%",
                18,
                new Color(0.93f, 0.73f, 0.34f, 1f));
            _zoomText.WidthSizePolicy = SizePolicy.StretchToParent;
            _zoomText.HeightSizePolicy = SizePolicy.Fixed;
            _zoomText.SuggestedHeight = 38f;
            _zoomText.VerticalAlignment = VerticalAlignment.Bottom;
            _zoomText.MarginBottom = 66f;
            _sidePanel.AddChild(_zoomText);

            TextWidget footer = CreateText(
                "MapFooter",
                "КОЛЕСО — МАСШТАБ  ·  ЛКМ — КУДА ИДТИ",
                18,
                new Color(0.68f, 0.54f, 0.32f, 1f));
            footer.WidthSizePolicy = SizePolicy.StretchToParent;
            footer.HeightSizePolicy = SizePolicy.Fixed;
            footer.SuggestedHeight = 48f;
            footer.VerticalAlignment = VerticalAlignment.Bottom;
            footer.MarginBottom = 24f;
            _sidePanel.AddChild(footer);
            _arenaButton = CreatePanelButton("MapGoArena", "БИТЬСЯ НА АРЕНЕ", 196f);
            _arenaButton.ClickEventHandlers.Add(_ =>
                DikozemieLobbyShellIntegration.OpenVenueFromMap(
                    DikozemieBattleVenue.Arena));
            _navalButton = CreatePanelButton("MapGoNaval", "НАНЯТЬСЯ МАТРОСОМ", 128f);
            _navalButton.ClickEventHandlers.Add(_ =>
                DikozemieLobbyShellIntegration.OpenVenueFromMap(
                    DikozemieBattleVenue.Naval));
            _goButton = CreatePanelButton("MapGo", "ИДТИ СЮДА", 264f);
            _goText = (TextWidget)_goButton.FindChild("MapGoText", true);
            _goButton.ClickEventHandlers.Add(_ => ToggleTravel());
            _boatButton = CreatePanelButton("MapBoat", "СЕСТЬ В ЛОДКУ", 332f);
            _boatText = (TextWidget)_boatButton.FindChild("MapBoatText", true);
            _boatButton.ClickEventHandlers.Add(_ => ToggleBoat());
            _legend = CreateText(
                "MapLegend",
                "Дорога — втрое быстрее, но там обязательно что-то случится.\n" +
                "Без дорог медленнее, в лесах сидят лучники. Ночью опаснее.\n" +
                "В горы нельзя, реки — по мостам и бродам. По воде — из порта: место на корабле или своя лодка.",
                16,
                new Color(0.68f, 0.60f, 0.46f, 1f));
            _legend.WidthSizePolicy = SizePolicy.StretchToParent;
            _legend.HeightSizePolicy = SizePolicy.Fixed;
            _legend.SuggestedHeight = 92f;
            _legend.MarginLeft = 30f;
            _legend.MarginRight = 30f;
            _legend.PositionYOffset = 370f;
            _legend.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            _legend.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
            _sidePanel.AddChild(_legend);
            BuildWorldButtons();
            BuildTownLayer();
            BuildRoadLayer();
            RestoreTravel();
            RefreshSidePanel();
        }

        private TextureWidget CreateFigure(string id, string provider)
        {
            var figure = new TextureWidget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                SuggestedWidth = 58f,
                SuggestedHeight = 58f,
                TextureProviderName = provider,
                DoNotAcceptEvents = true
            };
            AddChild(figure);
            return figure;
        }

        private static string HeroToken()
        {
            return DikozemieHeroProfile.TravelToken;
        }

        private void RestoreTravel()
        {
            float homeX = _homeNode?.Position?.X ?? SourceWidth * 0.5f;
            float homeY = _homeNode?.Position?.Y ?? SourceHeight * 0.5f;
            _travel = DikozemieTravelState.Load(HeroToken(), homeX, homeY, _homeNode?.Id);
            if (_travel.HasTarget)
            {
                _hasSelectedPoint = true;
                _selectedX = _travel.TargetX;
                _selectedY = _travel.TargetY;
                _selectedNode = DikozemieLobbyMapCatalog.Find(_travel.TargetNodeId);
                _route = DikozemieTravelRouter.Find(
                    _travel.X, _travel.Y, _selectedX, _selectedY, _travel.Boat);
                _routeIndex = 0;
                _travelNote = "Путь прерван, его можно продолжить.";
            }
            if (DikozemieRoads.IsNearLair(_travel.X, _travel.Y))
            {
                _lairCooldown = true;
            }
            ApplyTestHour();
        }

        // Tests: DIKOZEMIE_TEST_HOUR=<0..23> puts the clock at that hour.
        private void ApplyTestHour()
        {
            string wanted = Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_HOUR");
            if (_travel == null || string.IsNullOrEmpty(wanted) ||
                !int.TryParse(wanted, out int hour) || hour < 0 || hour > 23)
            {
                return;
            }
            float day = (float)Math.Floor(_travel.Hours / 24f);
            _travel.Hours = day * 24f + hour;
        }

        private void SelectPoint(float x, float y)
        {
            _selectedNode = null;
            _hasSelectedPoint = true;
            _selectedX = x;
            _selectedY = y;
            BuildPreview();
        }

        private void BuildPreview()
        {
            if (_travel == null || !_hasSelectedPoint)
            {
                return;
            }
            StopWalking();
            _travel.HasTarget = false;
            _route = DikozemieTravelRouter.Find(
                _travel.X, _travel.Y, _selectedX, _selectedY, _travel.Boat || _passagePick);
            _routeIndex = 0;
            _travelNote = string.Empty;
            RefreshSidePanel();
            LayoutContent();
        }

        private bool IsAtSelected()
        {
            if (_travel == null || !_hasSelectedPoint)
            {
                return false;
            }
            float dx = _travel.X - _selectedX;
            float dy = _travel.Y - _selectedY;
            return dx * dx + dy * dy < 24f * 24f;
        }

        private DikozemieLobbyMapNode NodeHere()
        {
            return DikozemieLobbyMapCatalog.Find(_travel?.NodeId);
        }

        private bool CanBoard()
        {
            if (_travel == null || _travel.Boat ||
                _travel.Vessel == DikozemieTravelState.NoVessel)
            {
                return false;
            }
            return NodeHere()?.HasPort == true ||
                (DikozemieTravelTerrain.AtAtlas(_travel.X, _travel.Y) &
                    DikozemieTravelTerrain.Port) != 0;
        }

        private bool CanDisembark()
        {
            return _travel != null && _travel.Boat &&
                DikozemieTravelRouter.TrySnap(_travel.X, _travel.Y, false, 7, out _, out _);
        }

        private void ToggleBoat()
        {
            if (_travel == null || _travelling)
            {
                return;
            }
            if (_travel.Boat)
            {
                if (!CanDisembark())
                {
                    return;
                }
                DikozemieTravelRouter.TrySnap(_travel.X, _travel.Y, false, 7, out int cx, out int cy);
                _travel.X = (cx + 0.5f) * DikozemieTravelTerrain.CellSize;
                _travel.Y = (cy + 0.5f) * DikozemieTravelTerrain.CellSize;
                _travel.Boat = false;
            }
            else
            {
                // Boats and passages are sold in the port of a town
                // (customer 2026-11-05): «Порт» in the town menu.
                if (!CanBoard())
                {
                    return;
                }
                _travel.Boat = true;
            }
            DikozemieTravelState.Save();
            if (_hasSelectedPoint)
            {
                BuildPreview();
            }
            _travelNote = _travel.Boat
                ? "Вы в своей " + (_travel.Vessel == DikozemieTravelState.Ship ? "ладье" : "лодке") +
                    ". Выберите на карте, куда плыть."
                : "Вы сошли на берег.";
            RefreshSidePanel();
        }

        private void ToggleTravel()
        {
            if (_travel == null)
            {
                return;
            }
            if (DikozemieRoads.IsCaptive && !_travelling)
            {
                OpenTown(DikozemieRoads.CaptiveTown);
                return;
            }
            if (DikozemieWorld.IsFollower && !_travelling)
            {
                _travelNote = "Отряд ведёт другой: куда он, туда и вы. Чтобы идти самому, покиньте отряд.";
                RefreshSidePanel();
                return;
            }
            if (_travelling)
            {
                StopWalking();
                _travel.HasTarget = true;
                _travelNote = "Вы остановились. Можно идти дальше.";
                DikozemieTravelState.Save();
                RefreshSidePanel();
                return;
            }
            if (_route == null || _route.Xs.Count < 2 || IsAtSelected())
            {
                return;
            }
            _travelling = true;
            _travelNote = string.Empty;
            _travel.HasTarget = true;
            _travel.TargetX = _selectedX;
            _travel.TargetY = _selectedY;
            _travel.TargetNodeId = _selectedNode?.Id ?? string.Empty;
            _travel.NodeId = string.Empty;
            DikozemieTravelState.Save();
            if (_zoom < 3f)
            {
                _zoom = 3f;
            }
            _untilFollow = 0f;
            RefreshSidePanel();
        }

        private void StopWalking()
        {
            _travelling = false;
        }

        private void TickTravel(float dt)
        {
            _untilFollow = Math.Max(0f, _untilFollow - dt);
            if (_travel != null && !string.Equals(
                    _travel.HeroToken, HeroToken(), StringComparison.Ordinal))
            {
                // A new hero was created while this screen stayed alive.
                _homeNode = DikozemieLobbyMapCatalog.FindHomeNode(DikozemieHeroProfile.CultureId);
                _travelling = false;
                _route = null;
                _hasSelectedPoint = false;
                _selectedNode = null;
                _travelNote = string.Empty;
                RestoreTravel();
                RefreshSidePanel();
            }
            if (_travel != null && IsVisible && TickFollow(dt))
            {
                return;
            }
            // A passage broken off by a sea fight or a restart goes on by
            // itself: a passenger has no «ПЛЫТЬ ДАЛЬШЕ». Found 2026-11-20: after
            // the pirates the hero stayed at sea 3 s short of Trondelag for good.
            if (!_travelling && _travel != null && _travel.IsPassenger && IsVisible &&
                !InformationManager.IsAnyInquiryActive())
            {
                ResumePassage();
            }
            if (!_travelling || _travel == null || _route == null ||
                !IsVisible || InformationManager.IsAnyInquiryActive())
            {
                return;
            }
            _boatBob += dt;
            byte cell = DikozemieTravelTerrain.AtAtlas(_travel.X, _travel.Y);
            DikozemieRoads.Advance(dt * DikozemieRoads.HoursPerTravelSecond);
            float budget = DikozemieTravelRouter.SpeedOf(cell, _travel.Boat) *
                DikozemieRoads.SpeedFactor(_travel.Boat) * dt;
            float walked = 0f;
            float startX = _travel.X;
            while (budget > 0f && _routeIndex < _route.Xs.Count - 1)
            {
                float toX = _route.Xs[_routeIndex + 1];
                float toY = _route.Ys[_routeIndex + 1];
                float dx = toX - _travel.X;
                float dy = toY - _travel.Y;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                if (length <= budget)
                {
                    _travel.X = toX;
                    _travel.Y = toY;
                    budget -= length;
                    walked += length;
                    _routeIndex++;
                    continue;
                }
                _travel.X += dx / length * budget;
                _travel.Y += dy / length * budget;
                walked += budget;
                budget = 0f;
            }
            _boatTurnX += _travel.X - startX;
            if (Math.Abs(_boatTurnX) > BoatTurnDistance)
            {
                // Small steps of a winding route must not flip the ship to and fro.
                _boatFacesLeft = _boatTurnX < 0f;
                _boatTurnX = 0f;
            }
            if (!_travel.Boat && CheckBridgeAndLair())
            {
                return;
            }
            CountRisk(walked, cell);
            _panelRefresh -= dt;
            if (_panelRefresh <= 0f && _travelling)
            {
                _panelRefresh = 1f;
                RefreshSidePanel();
            }
            _saveCountdown -= dt;
            if (_saveCountdown <= 0f)
            {
                _saveCountdown = 5f;
                DikozemieTravelState.Save();
            }
            if (_routeIndex >= _route.Xs.Count - 1)
            {
                Arrive();
            }
        }

        private void ResumePassage()
        {
            DikozemieLobbyMapNode port = DikozemieLobbyMapCatalog.Find(_travel.PassengerTo);
            if (port?.Position == null)
            {
                _travel.PassengerTo = string.Empty;
                DikozemieTravelState.Save();
                return;
            }
            if (_route == null || _route.Xs.Count < 2 || _selectedNode != port)
            {
                _selectedNode = port;
                _hasSelectedPoint = true;
                _selectedX = port.Position.X;
                _selectedY = port.Position.Y;
                _route = DikozemieTravelRouter.Find(_travel.X, _travel.Y, _selectedX, _selectedY, true);
                _routeIndex = 0;
            }
            if (_route == null || _route.Xs.Count < 2 || IsAtSelected())
            {
                // No way on by water from here: the ship puts in all the same.
                Arrive();
                return;
            }
            ToggleTravel();
            _travelNote = "Вы на борту. Корабль идёт в " + DisplayNodeName(port) + ".";
            DikozemieLobbyShellIntegration.TraceButtonAction("passage resumed to=" + port.Id);
            RefreshSidePanel();
        }

        private void Arrive()
        {
            StopWalking();
            _travel.HasTarget = false;
            DikozemieLobbyMapNode node = _selectedNode;
            if (_travel.IsPassenger)
            {
                DikozemieLobbyMapNode port = DikozemieLobbyMapCatalog.Find(_travel.PassengerTo) ?? node;
                _travel.PassengerTo = string.Empty;
                _travel.Boat = false;
                _route = null;
                if (port?.Position != null)
                {
                    _travel.NodeId = port.Id;
                    _travel.X = port.Position.X;
                    _travel.Y = port.Position.Y;
                }
                _hasSelectedPoint = false;
                _selectedNode = null;
                _travelNote = string.Empty;
                DikozemieTravelState.Save();
                DikozemieLobbyShellIntegration.TraceButtonAction("passage arrived node=" + port?.Id);
                OpenTown(port);
                _townNote = "Корабль пришёл в " + DisplayNodeName(port) + ". Вы сошли на берег.";
                RefreshSidePanel();
                return;
            }
            if (node != null && !_route.Snapped)
            {
                _travel.NodeId = node.Id;
                _travel.Boat = false;
                _travelNote = "Вы прибыли.";
            }
            else if (_route.Snapped)
            {
                _travelNote = _travel.Boat
                    ? "Ближе не подплыть. Можно высадиться на берег."
                    : "Дальше пути нет: горы или вода.";
            }
            else
            {
                _travelNote = "Вы на месте.";
            }
            _route = null;
            if (_selectedNode == null || _travel.NodeId != _selectedNode.Id)
            {
                // Nothing to describe at an empty spot: only the note remains.
                _hasSelectedPoint = false;
                _selectedNode = null;
            }
            DikozemieTravelState.Save();
            RefreshSidePanel();
            if (NodeHere() != null && !_travel.Boat)
            {
                // Like the singleplayer game: arriving opens the place.
                DikozemieLobbyMapNode arrived = NodeHere();
                string parting = DikozemieRoads.OnArrive(arrived);
                string gate = DikozemieRoads.GateToll(arrived);
                OpenTown(arrived);
                string note = string.Join("\n", new[] { parting, gate }.Where(line => !string.IsNullOrEmpty(line)));
                if (!string.IsNullOrEmpty(note))
                {
                    _townNote = note;
                    RefreshSidePanel();
                }
            }
        }

        private void CountRisk(float walked, byte cell)
        {
            if (walked <= 0f || DikozemieWorld.IsFollower)
            {
                return;
            }
            if (!_travel.Boat && (cell & DikozemieTravelTerrain.Road) != 0)
            {
                _travel.RoadSinceEvent += walked;
                if (_travel.RoadSinceEvent >= DikozemieTravelEvents.RoadEventEvery)
                {
                    _travel.RoadSinceEvent = 0f;
                    ShowEncounter(DrawHere(true));
                }
                return;
            }
            _travel.WildSinceEvent += walked;
            if (_travel.WildSinceEvent < DikozemieTravelEvents.WildCheckEvery)
            {
                return;
            }
            _travel.WildSinceEvent = 0f;
            if (_travel.Boat)
            {
                if (DikozemieTravelEvents.Roll(DikozemieTravelEvents.SeaChance))
                {
                    ShowEvent(DikozemieTravelEvents.DrawSea());
                }
                return;
            }
            DikozemieRoadEncounter wild = DrawHere(false);
            if (wild != null)
            {
                ShowEncounter(wild);
            }
        }

        private void ShowEvent(DikozemieTravelEvent card)
        {
            DikozemieLobbyShellIntegration.TraceButtonAction("travel event " + card.Title);
            bool spotted = (card.Kind == DikozemieTravelEventKind.Fight ||
                    card.Kind == DikozemieTravelEventKind.Naval) &&
                DikozemieTravelEvents.Roll(DikozemieTravelEvents.SpotFirstChance(ScoutingSkill()));
            string text = card.Text + (spotted
                ? "\n\nРазведка сработала: вы заметили их первыми и можете обойти стороной."
                : string.Empty);
            switch (card.Kind)
            {
                case DikozemieTravelEventKind.Fight:
                case DikozemieTravelEventKind.Naval:
                {
                    bool naval = card.Kind == DikozemieTravelEventKind.Naval;
                    bool canPay = card.Coins > 0 &&
                        DikozemieArmoryProgression.HeroResources >= card.Coins;
                    string decline = spotted
                        ? "ОБОЙТИ СТОРОНОЙ"
                        : canPay ? "ОТКУПИТЬСЯ: " + card.Coins + " МОН." : string.Empty;
                    Inquiry(
                        card.Title,
                        text,
                        "ПРИНЯТЬ БОЙ",
                        decline,
                        () => StartEventFight(card, naval),
                        () =>
                        {
                            if (!spotted && canPay)
                            {
                                DikozemieArmoryProgression.TrySpendHeroResources(card.Coins, out _, "bribe",
                                    card.Title);
                            }
                        });
                    break;
                }
                case DikozemieTravelEventKind.Toll:
                {
                    bool canPay = DikozemieArmoryProgression.HeroResources >= card.Coins;
                    Inquiry(
                        card.Title,
                        text,
                        canPay ? "ЗАПЛАТИТЬ: " + card.Coins + " МОН." : "НЕЧЕМ ПЛАТИТЬ — В БОЙ",
                        canPay ? "В БОЙ" : string.Empty,
                        () =>
                        {
                            if (canPay)
                            {
                                DikozemieArmoryProgression.TrySpendHeroResources(card.Coins, out _, "toll",
                                    card.Title);
                            }
                            else
                            {
                                StartEventFight(card, false);
                            }
                        },
                        () => StartEventFight(card, false));
                    break;
                }
                case DikozemieTravelEventKind.Gift:
                    Inquiry(card.Title, text, "ВЗЯТЬ: " + card.Coins + " МОН.", "МИМО",
                        () => DikozemieArmoryProgression.RefundHeroResources(card.Coins, out _, "road", card.Title),
                        null);
                    break;
                default:
                    Inquiry(card.Title, text, "ИДТИ ДАЛЬШЕ", string.Empty, null, null);
                    break;
            }
        }

        private static void Inquiry(
            string title, string text, string accept, string decline, Action onAccept, Action onDecline)
        {
            InformationManager.ShowInquiry(
                new InquiryData(
                    title,
                    text,
                    true,
                    !string.IsNullOrEmpty(decline),
                    accept,
                    decline,
                    onAccept,
                    onDecline),
                true,
                true);
        }

        private static bool _testTownWalkDone;

        // Tests: DIKOZEMIE_TEST_TOWN_WALK=<Streets|Tavern|Arena|Hall> starts
        // the town walk once the map opens; DIKOZEMIE_TEST_TOWN_NODE=<id>
        // walks that place instead of the one the hero stands in.
        private void TickTestTownWalk()
        {
            string wanted = Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_TOWN_WALK");
            if (_testTownWalkDone || !IsVisible || _travel == null || string.IsNullOrEmpty(wanted) ||
                TaleWorlds.MountAndBlade.Mission.Current != null)
            {
                return;
            }
            _testTownWalkDone = true;
            string nodeId = Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_TOWN_NODE");
            DikozemieLobbyMapNode node = string.IsNullOrEmpty(nodeId) ? NodeHere() : DikozemieLobbyMapCatalog.Find(nodeId);
            if (!Enum.TryParse(wanted, true, out DikozemieWalkPlace place))
            {
                place = DikozemieWalkPlace.Streets;
            }
            if (node == null || !DikozemieTownWalk.TryStart(node, place, null, out string error))
            {
                DikozemieLobbyShellIntegration.TraceButtonAction("test town walk not started node=" + (node?.Id ?? "none"));
            }
        }

        private static bool _testRoadCardDone;

        // Tests: DIKOZEMIE_TEST_ROAD_CARD=<kind> shows that card when the
        // map opens (Bandits, Catchers, Toll, Bridge, Peddler, Travellers,
        // Fugitive, Lair); DIKOZEMIE_TEST_ROAD_FIGHT=1 goes straight to the
        // fight of the card.
        private void TickTestRoadCard()
        {
            string wanted = Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_ROAD_CARD");
            if (_testRoadCardDone || !IsVisible || _travel == null || string.IsNullOrEmpty(wanted) ||
                TaleWorlds.MountAndBlade.Mission.Current != null || _townOpen)
            {
                return;
            }
            _testRoadCardDone = true;
            DikozemieRoadEncounter card;
            if (wanted == "Bridge")
            {
                card = DikozemieRoads.BuildBridge(_travel.X, _travel.Y, 1);
            }
            else if (wanted == "Lair")
            {
                if (!DikozemieRoads.HasBounty)
                {
                    DikozemieRoads.Accept(DikozemieRoads.OfferAt(DikozemieRoads.NearestTown(_travel.X, _travel.Y)));
                }
                card = DikozemieRoads.BuildLair();
            }
            else if (Enum.TryParse(wanted, out DikozemieRoadKind kind))
            {
                byte cell = DikozemieTravelTerrain.AtAtlas(_travel.X, _travel.Y);
                DikozemieLobbyMapNode near = DikozemieLobbyMapCatalog.FindNearestNode(_travel.X, _travel.Y);
                card = DikozemieRoads.Build(kind, DikozemieRoadFolk.FolkCulture(near?.Culture),
                    (cell & DikozemieTravelTerrain.Forest) != 0, false, false, DikozemieRoads.IsNight,
                    Environment.TickCount & 0xFFFFF, _travel.X, _travel.Y);
            }
            else
            {
                return;
            }
            ShowEncounter(card);
            if (card != null && Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_ROAD_FIGHT") == "1")
            {
                StartRoadFight(card, false);
            }
        }

        // A card for the hero's spot: the people of the land, the forest,
        // hills and coast around.
        private DikozemieRoadEncounter DrawHere(bool road)
        {
            float x = _travel.X;
            float y = _travel.Y;
            byte cell = DikozemieTravelTerrain.AtAtlas(x, y);
            bool forest = (cell & DikozemieTravelTerrain.Forest) != 0 ||
                DikozemieTravelTerrain.Near(x, y, DikozemieTravelTerrain.Forest, 2);
            bool hills = DikozemieTravelTerrain.Near(x, y, DikozemieTravelTerrain.Mountain, 6);
            bool coast = DikozemieTravelTerrain.Near(x, y, DikozemieTravelTerrain.Water, 8);
            return road
                ? DikozemieRoads.DrawRoad(x, y, forest, hills, coast)
                : DikozemieRoads.DrawWild(x, y, forest, hills, coast);
        }

        // A manned bridge stops the hero; so does the lair of his wanted man.
        private bool CheckBridgeAndLair()
        {
            int bridge = DikozemieTravelTerrain.BridgeAt(_travel.X, _travel.Y);
            if (bridge != 0 && bridge != _travel.LastBridge)
            {
                _travel.LastBridge = bridge;
                DikozemieRoadEncounter toll = DikozemieRoads.IsBridgeManned(bridge)
                    ? DikozemieRoads.BuildBridge(_travel.X, _travel.Y, bridge)
                    : null;
                if (toll != null)
                {
                    ShowEncounter(toll);
                    return true;
                }
            }
            bool nearLair = DikozemieRoads.IsNearLair(_travel.X, _travel.Y);
            if (_lairCooldown && !nearLair)
            {
                _lairCooldown = false;
            }
            if (nearLair && !_lairCooldown)
            {
                ShowEncounter(DikozemieRoads.BuildLair());
                return true;
            }
            return false;
        }

        private static string RoadStatus()
        {
            var lines = new List<string>();
            foreach (string line in new[]
                     {
                         DikozemieRoads.WoundText, DikozemieRoads.CompanyText, DikozemieRoads.BountyText
                     })
            {
                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }
            return lines.Count == 0 ? string.Empty : "\n" + string.Join("\n", lines);
        }

        private void StartEventFight(DikozemieTravelEvent card, bool naval)
        {
            StopWalking();
            _travelNote = "Путь прерван, его можно продолжить.";
            DikozemieTravelState.Save();
            RefreshSidePanel();
            DikozemieLobbyShellIntegration.StartRoadBattle(
                naval ? DikozemieBattleVenue.Naval : DikozemieBattleVenue.Arena,
                card.TeamSize);
        }

        private static int ScoutingSkill() =>
            DikozemieHeroProfile.GetEffectiveSkill("Scouting", 0);

        private static float VisionRadius() =>
            VisionBase + ScoutingSkill() * VisionPerScouting;

        private static string FormatTime(float seconds)
        {
            int total = Math.Max(1, (int)Math.Ceiling(seconds));
            return total >= 60
                ? (total / 60) + " мин " + (total % 60) + " с"
                : total + " с";
        }

        private ButtonWidget CreatePanelButton(string id, string text, float bottom)
        {
            var button = new ButtonWidget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedHeight = 58f,
                VerticalAlignment = VerticalAlignment.Bottom,
                MarginLeft = 30f,
                MarginRight = 30f,
                MarginBottom = bottom,
                DoNotPassEventsToChildren = true
            };
            button.AddChild(CreateSolid(id + "Frame", new Color(0.72f, 0.47f, 0.17f, 1f)));
            Widget fill = CreateSolid(id + "Fill", new Color(0.12f, 0.08f, 0.04f, 1f));
            fill.MarginLeft = 2f;
            fill.MarginRight = 2f;
            fill.MarginTop = 2f;
            fill.MarginBottom = 2f;
            button.AddChild(fill);
            TextWidget label = CreateText(id + "Text", text, 24, new Color(0.98f, 0.83f, 0.42f, 1f));
            label.WidthSizePolicy = SizePolicy.StretchToParent;
            label.HeightSizePolicy = SizePolicy.StretchToParent;
            button.AddChild(label);
            _sidePanel.AddChild(button);
            return button;
        }

        private void LayoutContent()
        {
            if (!_built || Size.X <= 0f || Size.Y <= 0f)
            {
                return;
            }

            GetViewport(out float viewportX, out float viewportY,
                out float availableWidth, out float availableHeight);
            GetFittedMapSize(availableWidth, availableHeight,
                out float fittedWidth, out float fittedHeight);
            float mapWidth = fittedWidth * _zoom;
            float mapHeight = fittedHeight * _zoom;
            ClampPan(mapWidth, mapHeight, availableWidth, availableHeight);
            float mapX = viewportX + ((availableWidth - mapWidth) * 0.5f) + _panX;
            float mapY = viewportY + ((availableHeight - mapHeight) * 0.5f) + _panY;

            _mapShadow.SuggestedWidth = mapWidth + 16f;
            _mapShadow.SuggestedHeight = mapHeight + 16f;
            _mapShadow.PositionXOffset = mapX - 8f;
            _mapShadow.PositionYOffset = mapY - 8f;

            _map.SuggestedWidth = mapWidth;
            _map.SuggestedHeight = mapHeight;
            _map.PositionXOffset = mapX;
            _map.PositionYOffset = mapY;

            LayoutDetailTiles(
                mapX,
                mapY,
                mapWidth,
                mapHeight,
                viewportX,
                viewportY,
                availableWidth,
                availableHeight);
            if ((_travelling && _untilFollow <= 0f || _centerOnHero) && _travel != null && _zoom > MinZoom)
            {
                // The figure stays in view on its way, unless the player is looking elsewhere.
                float wantX = viewportX + availableWidth * 0.5f;
                float wantY = viewportY + availableHeight * 0.5f;
                _panX += wantX - (mapX + _travel.X / SourceWidth * mapWidth);
                _panY += wantY - (mapY + _travel.Y / SourceHeight * mapHeight);
                ClampPan(mapWidth, mapHeight, availableWidth, availableHeight);
                mapX = viewportX + ((availableWidth - mapWidth) * 0.5f) + _panX;
                mapY = viewportY + ((availableHeight - mapHeight) * 0.5f) + _panY;
                _map.PositionXOffset = mapX;
                _map.PositionYOffset = mapY;
                _mapShadow.PositionXOffset = mapX - 8f;
                _mapShadow.PositionYOffset = mapY - 8f;
                LayoutDetailTiles(mapX, mapY, mapWidth, mapHeight, viewportX, viewportY,
                    availableWidth, availableHeight);
            }
            _centerOnHero = false;
            LayoutNight(mapX, mapY, mapWidth, mapHeight, viewportX, viewportY,
                availableWidth, availableHeight);
            LayoutVision(mapX, mapY, mapWidth, mapHeight, viewportX, viewportY,
                availableWidth, availableHeight);
            LayoutRoutePreview(mapX, mapY, mapWidth, mapHeight);
            LayoutSettlementMarkers(
                mapX,
                mapY,
                mapWidth,
                mapHeight,
                viewportX,
                viewportY,
                availableWidth,
                availableHeight);

            LayoutWorld(mapX, mapY, mapWidth, mapHeight, viewportX, viewportY,
                availableWidth, availableHeight);
            LayoutLands(mapX, mapY, mapWidth, mapHeight, viewportX, viewportY,
                availableWidth, availableHeight);
            LayoutFigure(mapX, mapY, mapWidth, mapHeight, viewportX, viewportY,
                availableWidth, availableHeight);
            LayoutLair(mapX, mapY, mapWidth, mapHeight, viewportX, viewportY,
                availableWidth, availableHeight);
            LayoutTown(viewportX, viewportY, availableWidth, availableHeight);
            LayoutRoad(viewportX, viewportY, availableWidth, availableHeight);
            float panelLeft = ViewWidth - SidePanelWidth - OuterMargin;
            _sideColumn.PositionXOffset = panelLeft;
            _sideColumn.PositionYOffset = 0f;
            _sideColumn.SuggestedWidth = ViewWidth - panelLeft;
            _sideColumn.SuggestedHeight = ViewHeight;
            _sidePanel.SuggestedWidth = SidePanelWidth;
            _sidePanel.SuggestedHeight = availableHeight;
            _sidePanel.PositionXOffset = ViewWidth - SidePanelWidth - OuterMargin;
            _sidePanel.PositionYOffset = OuterMargin;
            if (_zoomText != null)
            {
                _zoomText.Text = "МАСШТАБ " + (int)Math.Round(_zoom * 100f) + "%";
            }
        }

        private void LayoutVision(
            float mapX, float mapY, float mapWidth, float mapHeight,
            float viewportX, float viewportY, float viewportWidth, float viewportHeight)
        {
            if (_travel == null)
            {
                return;
            }
            float x = mapX + _travel.X / SourceWidth * mapWidth;
            float y = mapY + _travel.Y / SourceHeight * mapHeight;
            float radius = VisionRadius() / SourceWidth * mapWidth;
            _visionRing.SuggestedWidth = radius * 2f;
            _visionRing.SuggestedHeight = radius * 2f;
            _visionRing.PositionXOffset = x - radius;
            _visionRing.PositionYOffset = y - radius;

            // While travelling everything beyond sight goes dark, like in Kingdom Come.
            bool dark = _travelling;
            _visionMask.IsVisible = dark;
            float side = radius / VisionMaskHole;
            float left = x - side * 0.5f;
            float top = y - side * 0.5f;
            float right = left + side;
            float bottom = top + side;
            _visionMask.SuggestedWidth = side;
            _visionMask.SuggestedHeight = side;
            _visionMask.PositionXOffset = left;
            _visionMask.PositionYOffset = top;
            float viewRight = ViewWidth;
            float viewBottom = ViewHeight;
            SetSheet(0, 0f, 0f, viewRight, top, dark);
            SetSheet(1, 0f, bottom, viewRight, viewBottom, dark);
            SetSheet(2, 0f, Math.Max(top, 0f), left, Math.Min(bottom, viewBottom), dark);
            SetSheet(3, right, Math.Max(top, 0f), viewRight, Math.Min(bottom, viewBottom), dark);
        }

        private void SetSheet(int index, float x0, float y0, float x1, float y1, bool visible)
        {
            Widget sheet = _visionSheets[index];
            bool show = visible && x1 > x0 && y1 > y0;
            sheet.IsVisible = show;
            if (!show)
            {
                return;
            }
            sheet.PositionXOffset = x0;
            sheet.PositionYOffset = y0;
            sheet.SuggestedWidth = x1 - x0;
            sheet.SuggestedHeight = y1 - y0;
        }

        private void LayoutFigure(
            float mapX, float mapY, float mapWidth, float mapHeight,
            float viewportX, float viewportY, float viewportWidth, float viewportHeight)
        {
            if (_travel == null)
            {
                return;
            }
            float x = mapX + _travel.X / SourceWidth * mapWidth;
            float y = mapY + _travel.Y / SourceHeight * mapHeight;
            bool inView = x >= viewportX - 30f && x <= viewportX + viewportWidth + 30f &&
                y >= viewportY - 30f && y <= viewportY + viewportHeight + 30f;
            TextureWidget shown = !_travel.Boat
                ? _heroFigure
                : _boatFacesLeft ? _boatFigureLeft : _boatFigure;
            _heroFigure.IsVisible = inView && ReferenceEquals(shown, _heroFigure);
            _boatFigure.IsVisible = inView && ReferenceEquals(shown, _boatFigure);
            _boatFigureLeft.IsVisible = inView && ReferenceEquals(shown, _boatFigureLeft);
            _visionRing.IsVisible = inView;
            shown.PositionXOffset = x - shown.SuggestedWidth * 0.5f;
            // The figure stands on its point: its base, not its middle. A ship
            // sits on its waterline and rides the swell while under way.
            shown.PositionYOffset = _travel.Boat
                ? y - shown.SuggestedHeight * 0.72f +
                    (_travelling ? (float)Math.Sin(_boatBob * 2.2f) * 1.6f : 0f)
                : y - shown.SuggestedHeight * 0.88f;
            _hereLabel.IsVisible = inView;
            _hereLabel.PositionXOffset = x - _hereLabel.SuggestedWidth * 0.5f;
            _hereLabel.PositionYOffset = _travel.Boat
                ? y - shown.SuggestedHeight * 0.72f - 24f
                : y - shown.SuggestedHeight - 24f;
        }

        private void LayoutDetailTiles(
            float mapX,
            float mapY,
            float mapWidth,
            float mapHeight,
            float viewportX,
            float viewportY,
            float viewportWidth,
            float viewportHeight)
        {
            bool detailEnabled = _zoom >= DetailZoomThreshold;
            bool fineEnabled = detailEnabled && mapWidth / SourceWidth >= FineTileScale;
            float viewportRight = viewportX + viewportWidth;
            float viewportBottom = viewportY + viewportHeight;
            foreach (MapTile tile in _detailTiles)
            {
                float tileX = mapX + (tile.AtlasX / SourceWidth * mapWidth);
                float tileY = mapY + (tile.AtlasY / SourceHeight * mapHeight);
                float tileWidth = tile.PixelWidth / SourceWidth * mapWidth;
                float tileHeight = tile.PixelHeight / SourceHeight * mapHeight;
                bool visible = (tile.Fine ? fineEnabled : detailEnabled) &&
                    tileX < viewportRight &&
                    tileY < viewportBottom &&
                    tileX + tileWidth > viewportX &&
                    tileY + tileHeight > viewportY;

                tile.Widget.IsVisible = visible;
                if (!visible)
                {
                    if (!string.IsNullOrEmpty(tile.Widget.TextureProviderName))
                    {
                        tile.Widget.OnClearTextureProvider();
                        tile.Widget.TextureProviderName = string.Empty;
                    }
                    continue;
                }

                if (string.IsNullOrEmpty(tile.Widget.TextureProviderName))
                {
                    if (_detailLoadCooldown > 0f)
                    {
                        continue;
                    }
                    tile.Widget.TextureProviderName = tile.ProviderName;
                    // Bannerlord decodes CreateFromMemory textures on the UI
                    // thread. Stagger tile creation instead of decoding the
                    // entire visible set in one render frame.
                    _detailLoadCooldown = 0.08f;
                }
                tile.Widget.SuggestedWidth = tileWidth + 0.75f;
                tile.Widget.SuggestedHeight = tileHeight + 0.75f;
                tile.Widget.PositionXOffset = tileX;
                tile.Widget.PositionYOffset = tileY;
            }
        }

        private void GetViewport(
            out float x,
            out float y,
            out float width,
            out float height)
        {
            x = OuterMargin;
            y = OuterMargin;
            width = Math.Max(320f, ViewWidth - SidePanelWidth - (OuterMargin * 3f));
            height = Math.Max(240f, ViewHeight - (OuterMargin * 2f));
        }

        private static void GetFittedMapSize(
            float viewportWidth,
            float viewportHeight,
            out float width,
            out float height)
        {
            float scale = Math.Min(
                viewportWidth / SourceWidth,
                viewportHeight / SourceHeight);
            width = SourceWidth * scale;
            height = SourceHeight * scale;
        }

        private void GetMapRectangle(
            out float x,
            out float y,
            out float width,
            out float height)
        {
            GetViewport(out float viewportX, out float viewportY,
                out float viewportWidth, out float viewportHeight);
            GetFittedMapSize(viewportWidth, viewportHeight,
                out float fittedWidth, out float fittedHeight);
            width = fittedWidth * _zoom;
            height = fittedHeight * _zoom;
            ClampPan(width, height, viewportWidth, viewportHeight);
            x = viewportX + (viewportWidth - width) * 0.5f + _panX;
            y = viewportY + (viewportHeight - height) * 0.5f + _panY;
        }

        private void ClampPan(
            float mapWidth,
            float mapHeight,
            float viewportWidth,
            float viewportHeight)
        {
            float limitX = Math.Max(0f, (mapWidth - viewportWidth) * 0.5f);
            float limitY = Math.Max(0f, (mapHeight - viewportHeight) * 0.5f);
            _panX = Math.Max(-limitX, Math.Min(limitX, _panX));
            _panY = Math.Max(-limitY, Math.Min(limitY, _panY));
            if (_zoom <= MinZoom)
            {
                _panX = 0f;
                _panY = 0f;
            }
        }

        private MapMarker CreateSettlementMarker(DikozemieLobbyMapNode node)
        {
            return new MapMarker { Node = node };
        }

        private void EnsureSettlementMarkerWidgets(MapMarker marker)
        {
            if (marker == null || marker.Node == null || marker.Dot != null)
            {
                return;
            }
            DikozemieLobbyMapNode node = marker.Node;
            Color cultureColor = GetCultureColor(node.Culture);
            marker.Halo = new Widget(Context)
            {
                Id = "SettlementHalo_" + node.Id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteCircle"),
                Color = new Color(0.08f, 0.035f, 0.012f, 0.72f),
                DoNotAcceptEvents = true
            };
            AddChild(marker.Halo);

            marker.Dot = new Widget(Context)
            {
                Id = "SettlementDot_" + node.Id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                Sprite = Context.SpriteData.GetSprite(
                    IsCastle(node) ? "BlankWhiteSquare_9" : "BlankWhiteCircle"),
                Color = cultureColor,
                DoNotAcceptEvents = true
            };
            AddChild(marker.Dot);

            if (!IsTown(node) && !IsCastle(node))
            {
                marker.Icon = new TextureWidget(Context)
                {
                    Id = "SettlementIcon_" + node.Id,
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    TextureProviderName = "DikozemieMapIconVillageTextureProvider",
                    DoNotAcceptEvents = true
                };
                AddChild(marker.Icon);
            }

            marker.Label = CreateText(
                "SettlementLabel_" + node.Id,
                DisplayNodeName(node),
                IsTown(node) ? 17 : 15,
                new Color(0.96f, 0.88f, 0.69f, 1f));
            marker.Label.WidthSizePolicy = SizePolicy.Fixed;
            marker.Label.HeightSizePolicy = SizePolicy.Fixed;
            marker.Label.SuggestedWidth = 220f;
            marker.Label.SuggestedHeight = 30f;
            marker.Label.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            marker.Label.Brush.FontColor = new Color(0.16f, 0.09f, 0.04f, 1f);
            marker.Label.Brush.DefaultStyle.TextGlowColor = new Color(0.98f, 0.92f, 0.78f, 1f);
            marker.Label.Brush.DefaultStyle.TextGlowRadius = 0.2f;
            marker.Label.Brush.DefaultStyle.TextBlur = 0.6f;
            AddChild(marker.Label);
            RaiseWorldLayer();
            RaiseLandLayer();
            _lairHalo?.SetSiblingIndex(ChildCount - 1);
            _lairDot?.SetSiblingIndex(ChildCount - 1);
            _lairLabel?.SetSiblingIndex(ChildCount - 1);
            _heroFigure.SetSiblingIndex(ChildCount - 1);
            _boatFigure.SetSiblingIndex(ChildCount - 1);
            _boatFigureLeft.SetSiblingIndex(ChildCount - 1);
            _hereLabel.SetSiblingIndex(ChildCount - 1);
            _town?.SetSiblingIndex(ChildCount - 1);
            _road?.SetSiblingIndex(ChildCount - 1);
            _sideColumn.SetSiblingIndex(ChildCount - 1);
            _sidePanel.SetSiblingIndex(ChildCount - 1);
        }

        private void LayoutSettlementMarkers(
            float mapX,
            float mapY,
            float mapWidth,
            float mapHeight,
            float viewportX,
            float viewportY,
            float viewportWidth,
            float viewportHeight)
        {
            float viewportRight = viewportX + viewportWidth;
            float viewportBottom = viewportY + viewportHeight;
            PlaceLabels(mapWidth, mapHeight);
            foreach (MapMarker marker in _markers)
            {
                DikozemieLobbyMapNode node = marker.Node;
                if (node?.Position == null)
                {
                    continue;
                }
                float x = mapX + ((node.Position.X / SourceWidth) * mapWidth);
                float y = mapY + ((node.Position.Y / SourceHeight) * mapHeight);
                bool visible = IsNodeVisible(node) &&
                    x >= viewportX - 28f && x <= viewportRight + 28f &&
                    y >= viewportY - 28f && y <= viewportBottom + 28f;
                if (visible && _travelling && _travel != null && !IsTown(node) &&
                    !ReferenceEquals(node, _selectedNode))
                {
                    float seenX = node.Position.X - _travel.X;
                    float seenY = node.Position.Y - _travel.Y;
                    float sight = VisionRadius() * 1.1f;
                    visible = seenX * seenX + seenY * seenY <= sight * sight;
                }
                if (!visible)
                {
                    SetMarkerVisible(marker, false);
                    continue;
                }

                EnsureSettlementMarkerWidgets(marker);
                bool isSelected = ReferenceEquals(node, _selectedNode);
                bool passagePort = IsPassagePort(node);
                float dotSize = IsTown(node) ? 12f : IsCastle(node) ? 9f : 7f;
                if (isSelected)
                {
                    dotSize += 4f;
                }
                float haloSize = dotSize + (isSelected ? 12f : passagePort ? 14f : 5f);
                marker.Halo.SuggestedWidth = haloSize;
                marker.Halo.SuggestedHeight = haloSize;
                marker.Halo.PositionXOffset = x - (haloSize * 0.5f);
                marker.Halo.PositionYOffset = y - (haloSize * 0.5f);
                marker.Halo.Color = isSelected
                    ? new Color(0.98f, 0.70f, 0.16f, 0.90f)
                    : passagePort
                        ? new Color(0.35f, 0.80f, 1f, 0.95f)
                        : new Color(0.08f, 0.035f, 0.012f, 0.72f);
                marker.Dot.SuggestedWidth = dotSize;
                marker.Dot.SuggestedHeight = dotSize;
                marker.Dot.PositionXOffset = x - (dotSize * 0.5f);
                marker.Dot.PositionYOffset = y - (dotSize * 0.5f);
                marker.Label.PositionXOffset = x + marker.LabelDx;
                marker.Label.PositionYOffset = y + marker.LabelDy;
                marker.Halo.IsVisible = marker.Icon == null || isSelected;
                marker.Dot.IsVisible = marker.Icon == null;
                if (_paintedMap)
                {
                    // The place is painted: a dot only while the drawing is too
                    // small to make out, a ring around it when it is chosen.
                    float painted = PaintedWidth(node) * mapWidth / SourceWidth;
                    bool small = painted < (IsTown(node) || IsCastle(node) ? 18f : 12f);
                    marker.Dot.IsVisible = small;
                    if ((isSelected || passagePort) && !small)
                    {
                        float ring = painted * 1.05f;
                        marker.Halo.SuggestedWidth = ring;
                        marker.Halo.SuggestedHeight = ring;
                        marker.Halo.PositionXOffset = x - ring * 0.5f;
                        marker.Halo.PositionYOffset = y - painted * 0.22f - ring * 0.5f;
                        marker.Halo.Color = isSelected
                            ? new Color(0.98f, 0.70f, 0.16f, 0.35f)
                            : new Color(0.35f, 0.80f, 1f, 0.35f);
                    }
                    marker.Halo.IsVisible = isSelected || passagePort || small;
                    if (marker.Icon != null)
                    {
                        marker.Icon.IsVisible = false;
                    }
                }
                else if (marker.Icon != null)
                {
                    float iconSize = Math.Min(44f, 16f + _zoom * 3.2f);
                    marker.Icon.IsVisible = true;
                    marker.Icon.SuggestedWidth = iconSize;
                    marker.Icon.SuggestedHeight = iconSize;
                    marker.Icon.PositionXOffset = x - iconSize * 0.5f;
                    marker.Icon.PositionYOffset = y - iconSize * 0.5f;
                }
                marker.Label.IsVisible = marker.LabelShown;
            }
        }

        private float MarkerRadius(DikozemieLobbyMapNode node)
        {
            float old = IsTown(node) || IsCastle(node)
                ? (IsTown(node) ? 9f : 7f)
                : Math.Min(44f, 16f + _zoom * 3.2f) * 0.5f;
            if (!_paintedMap || _labelsMapWidth <= 0f)
            {
                return old;
            }
            // the painted place: labels keep clear of the drawing
            return Math.Max(IsTown(node) || IsCastle(node) ? old : 5f,
                PaintedWidth(node) * _labelsMapWidth / SourceWidth * 0.5f);
        }

        private float PaintedWidth(DikozemieLobbyMapNode node)
        {
            return IsTown(node) ? PaintedTown : IsCastle(node) ? PaintedCastle : PaintedVillage;
        }

        private static bool BoxIsFree(List<float> boxes, float left, float top, float right, float bottom,
            out float overlap)
        {
            overlap = 0f;
            for (int index = 0; index < boxes.Count; index += 4)
            {
                float w = Math.Min(right, boxes[index + 2]) - Math.Max(left, boxes[index]);
                float h = Math.Min(bottom, boxes[index + 3]) - Math.Max(top, boxes[index + 1]);
                if (w > 0f && h > 0f)
                {
                    overlap += w * h;
                }
            }
            return overlap <= 0f;
        }

        // Chooses a side for every label: to the right when there is room,
        // otherwise wherever it covers neither a marker nor another label.
        // The sizes do not depend on panning, so this runs only when the zoom,
        // the selection or the standing figure changes.
        private void PlaceLabels(float mapWidth, float mapHeight)
        {
            float figureX = _travelling || _travel == null ? 0f : _travel.X;
            float figureY = _travelling || _travel == null ? 0f : _travel.Y;
            bool boat = _travel?.Boat == true;
            if (Math.Abs(_labelsMapWidth - mapWidth) < 0.01f &&
                ReferenceEquals(_labelsSelected, _selectedNode) &&
                Math.Abs(_labelsFigureX - figureX) < 1f &&
                Math.Abs(_labelsFigureY - figureY) < 1f &&
                _labelsBoat == boat)
            {
                return;
            }
            _labelsMapWidth = mapWidth;
            _labelsSelected = _selectedNode;
            _labelsFigureX = figureX;
            _labelsFigureY = figureY;
            _labelsBoat = boat;

            float scaleX = mapWidth / SourceWidth;
            float scaleY = mapHeight / SourceHeight;
            _labelBoxes.Clear();
            _labelOrder.Clear();
            foreach (MapMarker marker in _markers)
            {
                marker.LabelShown = false;
                DikozemieLobbyMapNode node = marker.Node;
                if (node?.Position == null || !IsNodeVisible(node))
                {
                    continue;
                }
                float px = node.Position.X * scaleX;
                float py = node.Position.Y * scaleY;
                float radius = MarkerRadius(node);
                _labelBoxes.Add(px - radius);
                _labelBoxes.Add(py - radius);
                _labelBoxes.Add(px + radius);
                _labelBoxes.Add(py + radius);
                bool selected = ReferenceEquals(node, _selectedNode);
                if (selected || IsLabelVisible(node))
                {
                    marker.Rank = selected ? 0 : IsTown(node) ? 1 : IsCastle(node) ? 2 : 3;
                    _labelOrder.Add(marker);
                }
            }
            if (_travel != null && !_travelling)
            {
                // The figure and its "you are here" stand where they stand.
                float px = _travel.X * scaleX;
                float py = _travel.Y * scaleY;
                float half = boat ? BoatFigureSize * 0.5f : 26f;
                float height = boat ? BoatFigureSize * 0.72f : 52f;
                _labelBoxes.Add(px - Math.Max(half, 46f));
                _labelBoxes.Add(py - height - 26f);
                _labelBoxes.Add(px + Math.Max(half, 46f));
                _labelBoxes.Add(py - height);
                _labelBoxes.Add(px - half);
                _labelBoxes.Add(py - height);
                _labelBoxes.Add(px + half);
                _labelBoxes.Add(py + 8f);
            }
            _labelOrder.Sort((a, b) => a.Rank.CompareTo(b.Rank));

            foreach (MapMarker marker in _labelOrder)
            {
                DikozemieLobbyMapNode node = marker.Node;
                float px = node.Position.X * scaleX;
                float py = node.Position.Y * scaleY;
                float radius = MarkerRadius(node);
                int fontSize = IsTown(node) ? 17 : 15;
                float width = DisplayNodeName(node).Length * fontSize * 0.47f + 4f;
                float height = fontSize + 4f;
                float near = radius + 2f;
                float[] lefts =
                {
                    px + near, px - near - width, px - width * 0.5f, px - width * 0.5f,
                    px + near, px + near, px - near - width, px - near - width
                };
                float[] tops =
                {
                    py - height * 0.5f, py - height * 0.5f, py - near - height, py + near,
                    py - near - height * 0.6f, py + near - height * 0.4f,
                    py - near - height * 0.6f, py + near - height * 0.4f
                };
                int best = -1;
                float bestOverlap = float.MaxValue;
                for (int slot = 0; slot < lefts.Length; slot++)
                {
                    // A label's own marker is in the list too, but no slot touches it.
                    if (BoxIsFree(_labelBoxes, lefts[slot], tops[slot], lefts[slot] + width,
                            tops[slot] + height, out float overlap))
                    {
                        best = slot;
                        bestOverlap = 0f;
                        break;
                    }
                    if (overlap < bestOverlap)
                    {
                        best = slot;
                        bestOverlap = overlap;
                    }
                }
                // Towns and the chosen place keep their name; a crowded castle
                // or village waits until the map is closer.
                if (bestOverlap > 0f && marker.Rank > 1)
                {
                    continue;
                }
                marker.LabelShown = true;
                marker.LabelDx = lefts[best] - px;
                marker.LabelDy = tops[best] + height * 0.5f - 15f - py;
                _labelBoxes.Add(lefts[best]);
                _labelBoxes.Add(tops[best]);
                _labelBoxes.Add(lefts[best] + width);
                _labelBoxes.Add(tops[best] + height);
            }
        }

        private bool IsNodeVisible(DikozemieLobbyMapNode node)
        {
            if (IsTown(node))
            {
                return true;
            }
            if (IsCastle(node))
            {
                return _zoom >= 2.2f;
            }
            return _zoom >= 1.5f;
        }

        private bool IsLabelVisible(DikozemieLobbyMapNode node)
        {
            if (IsTown(node))
            {
                return _zoom >= TownLabelZoom;
            }
            if (IsCastle(node))
            {
                return _zoom >= CastleLabelZoom;
            }
            return _zoom >= VillageLabelZoom;
        }

        private static void SetMarkerVisible(MapMarker marker, bool visible)
        {
            if (marker?.Halo != null)
            {
                marker.Halo.IsVisible = visible;
            }
            if (marker?.Dot != null)
            {
                marker.Dot.IsVisible = visible;
            }
            if (marker?.Label != null)
            {
                marker.Label.IsVisible = visible;
            }
            if (marker?.Icon != null)
            {
                marker.Icon.IsVisible = visible;
            }
        }

        private void SelectNode(DikozemieLobbyMapNode node)
        {
            if (node?.Position == null)
            {
                return;
            }
            _selectedNode = node;
            _hasSelectedPoint = true;
            _selectedX = node.Position.X;
            _selectedY = node.Position.Y;
            BuildPreview();
        }

        private void RefreshSidePanel()
        {
            if (_title == null || _description == null || _goButton == null || _travel == null ||
                _enterButton == null)
            {
                return;
            }
            if (_roadOpen)
            {
                RefreshRoadPanel();
                return;
            }
            if (_siegeOpen)
            {
                RefreshSiegePanel();
                return;
            }
            if (_clanOpen)
            {
                if (_chatOpen)
                {
                    SetMapPanelVisible(false);
                    HideTownOptions();
                    HideWorldButtons();
                    _optionCount = 0;
                    FillChatPanel(out string chatText);
                    _description.Text = chatText;
                    StackOptions();
                    return;
                }
                RefreshClanPanel();
                return;
            }
            if (_townOpen)
            {
                RefreshTownPanel();
                return;
            }
            if (_passagePick)
            {
                RefreshPassagePanel();
                return;
            }
            if (_worldPick != null && RefreshWorldCard())
            {
                return;
            }
            HideWorldButtons();
            HideTownOptions();
            SetMapPanelVisible(true);
            _legend.IsVisible = string.IsNullOrEmpty(RoadStatus()) && string.IsNullOrEmpty(_travelNote);
            DikozemieLobbyMapNode here = NodeHere();
            bool atNode = here != null && !_travelling;
            bool heroesNear = DikozemieWorld.Players.Any(p => string.IsNullOrEmpty(p.In) &&
                (DikozemieWorld.Party == null || p.Party != DikozemieWorld.Party.Id));
            string location = atNode
                ? "Вы в: " + DisplayNodeName(here) + "."
                : _travelling
                    ? "Вы в пути."
                    : _travel.Boat ? "Вы в море." : heroesNear ? "Рядом другие герои." : "Вокруг никого.";
            string status = RoadStatus();
            string sight = DikozemieRoads.ClockText + " · обзор " + (int)VisionRadius() + "." + status + WorldLines();
            // Only a map on screen takes the note: the arena page keeps this
            // widget alive behind it.
            string note = IsRecursivelyVisible() ? DikozemieRoads.TakeNote() : string.Empty;
            if (!string.IsNullOrEmpty(note))
            {
                _travelNote = note;
            }

            if (!_hasSelectedPoint)
            {
                _title.Text = "КАРТА ДИКОЗЕМЬЯ";
                // The text box has a fixed height above the legend: a note
                // replaces the general hint (the footer repeats it anyway),
                // so two or three lines of it never run into the legend.
                _description.Text = location + "\n" + sight +
                    (!string.IsNullOrEmpty(_travelNote)
                        ? "\n\n" + _travelNote
                        : string.IsNullOrEmpty(status)
                            ? "\n\nНажмите на любое место карты: появится путь." +
                              "\nКолесо приближает: появятся замки и деревни."
                            : string.Empty);
            }
            else
            {
                _title.Text = _selectedNode != null
                    ? DisplayNodeName(_selectedNode).ToUpperInvariant()
                    : DescribeTerrainTitle();
                string kind = _selectedNode != null
                    ? GetKindName(_selectedNode) + " · " + GetCultureName(_selectedNode.Culture) +
                        (_selectedNode.HasPort ? " · ПОРТ" : string.Empty) + "\n\n"
                    : string.Empty;
                string routeText;
                if (IsAtSelected())
                {
                    // "You have arrived" already says it.
                    routeText = string.IsNullOrEmpty(_travelNote) ? "Вы уже здесь." : string.Empty;
                }
                else if (_route == null)
                {
                    routeText = _travel.Boat
                        ? "Туда не доплыть."
                        : _travel.OwnsBoat
                            ? "Туда не дойти: путь перекрывают горы или вода. В море — на вашей лодке из порта."
                            : "Туда не дойти: путь перекрывают горы или вода. Лодку продают в порту.";
                }
                else
                {
                    float left = _route.Xs.Count < 2
                        ? 0f
                        : _route.Seconds * (1f - (float)_routeIndex / (_route.Xs.Count - 1)) /
                            Math.Max(0.1f, DikozemieRoads.SpeedFactor(_travel.Boat));
                    routeText = (_travel.Boat ? "По воде: " : "Пешком: ") +
                        FormatTime(left) +
                        (_travel.Boat ? "." : ", по дорогам " + (int)Math.Round(_route.RoadShare * 100f) + "%.");
                    if (_route.Snapped)
                    {
                        routeText += _travel.Boat
                            ? "\nПойдём к ближайшему берегу."
                            : "\nТуда нельзя, пойдём до ближайшего места.";
                    }
                }
                _description.Text = kind + location +
                    (string.IsNullOrEmpty(routeText) ? string.Empty : "\n" + routeText) +
                    LandCardLines(_selectedNode != null && (IsTown(_selectedNode) || IsCastle(_selectedNode) ||
                        DikozemieClans.LandOf(DikozemieClans.SeatOf(_selectedNode)) != null) ? _selectedNode : null) +
                    (string.IsNullOrEmpty(_travelNote) ? string.Empty : "\n\n" + _travelNote);
            }

            // The legend stands under the text: a long text hides it.
            if (_legend.IsVisible && EstimateLines(_description.Text, 36) > 8)
            {
                _legend.IsVisible = false;
            }
            bool canWalk = _route != null && _hasSelectedPoint && !IsAtSelected();
            bool passenger = _travel.IsPassenger;
            bool follower = DikozemieWorld.IsFollower;
            // A passenger cannot stop the ship halfway; a member of a party
            // walks where its leader walks.
            _goButton.IsVisible = !passenger && !follower && (_travelling || canWalk);
            string verb = _travel.Boat ? "ПЛЫТЬ" : "ИДТИ";
            _goText.Text = _travelling
                ? "ОСТАНОВИТЬСЯ"
                : verb + (_travel.HasTarget ? " ДАЛЬШЕ" : " СЮДА");
            _boatButton.IsVisible = !passenger && !follower && !_travelling && (CanBoard() || CanDisembark());
            _boatText.Text = _travel.Boat
                ? "ВЫСАДИТЬСЯ НА БЕРЕГ"
                : _travel.Vessel == DikozemieTravelState.Ship ? "СЕСТЬ В ЛАДЬЮ" : "СЕСТЬ В ЧЕЛН";
            // The arena, the market and the docks are inside the place now:
            // one button leads in, as in the singleplayer game.
            _arenaButton.IsVisible = false;
            _navalButton.IsVisible = false;
            _enterButton.IsVisible = atNode && !_travel.Boat;
            _enterText.Text = IsTown(here)
                ? "ВОЙТИ В ГОРОД"
                : IsCastle(here) ? "К ВОРОТАМ ЗАМКА" : "ВОЙТИ В ДЕРЕВНЮ";
            _worldParty.IsVisible = DikozemieWorld.InParty;
            float stack = 128f;
            // The way somewhere else is the main action: it stays at the bottom.
            foreach (ButtonWidget button in new[] { _goButton, _enterButton, _boatButton, _worldParty })
            {
                if (button.IsVisible)
                {
                    button.MarginBottom = stack;
                    stack += 68f;
                }
            }
            // Stage 6: a castle or town picked near the hero can be besieged.
            DikozemieLobbyMapNode walls = _hasSelectedPoint && _selectedNode != null ? _selectedNode : atNode ? here : null;
            if (!PlaceSiegeButton(walls, ref stack) && walls == null)
            {
                _siegeButton.IsVisible = false;
            }
        }

        private static int EstimateLines(string text, int perLine)
        {
            int lines = 0;
            foreach (string paragraph in (text ?? string.Empty).Split('\n'))
            {
                lines += Math.Max(1, (paragraph.Length + perLine - 1) / perLine);
            }
            return lines;
        }

        private string DescribeTerrainTitle()
        {
            byte cell = DikozemieTravelTerrain.AtAtlas(_selectedX, _selectedY);
            if ((cell & DikozemieTravelTerrain.Road) != 0)
            {
                return "ДОРОГА";
            }
            if ((cell & DikozemieTravelTerrain.Water) != 0)
            {
                return "ВОДА";
            }
            if ((cell & DikozemieTravelTerrain.Mountain) != 0)
            {
                return "ГОРЫ";
            }
            return (cell & DikozemieTravelTerrain.Forest) != 0 ? "ЛЕС" : "РАВНИНА";
        }

        private void LayoutRoutePreview(
            float mapX,
            float mapY,
            float mapWidth,
            float mapHeight)
        {
            if (_route == null || _route.Xs.Count < 2 || _travel == null)
            {
                HideRoutePreview();
                return;
            }
            float scaleX = mapWidth / SourceWidth;
            float scaleY = mapHeight / SourceHeight;
            var points = new List<Tuple<float, float>>(_route.Xs.Count + 1);
            points.Add(Tuple.Create(mapX + _travel.X * scaleX, mapY + _travel.Y * scaleY));
            for (int index = _routeIndex + 1; index < _route.Xs.Count; index++)
            {
                points.Add(Tuple.Create(
                    mapX + _route.Xs[index] * scaleX,
                    mapY + _route.Ys[index] * scaleY));
            }
            if (points.Count < 2)
            {
                HideRoutePreview();
                return;
            }

            float total = 0f;
            for (int index = 1; index < points.Count; index++)
            {
                float dx = points[index].Item1 - points[index - 1].Item1;
                float dy = points[index].Item2 - points[index - 1].Item2;
                total += (float)Math.Sqrt((dx * dx) + (dy * dy));
            }
            int count = Math.Min(
                _routeSegments.Count,
                Math.Max(2, (int)Math.Floor(total / RouteSpacing) + 1));
            Color color = _route.Boat
                ? new Color(0.35f, 0.80f, 1f, 1f)
                : new Color(0.98f, 0.76f, 0.22f, 1f);
            for (int index = 0; index < _routeSegments.Count; index++)
            {
                bool visible = index < count;
                _routeSegments[index].IsVisible = visible;
                _routeHalos[index].IsVisible = visible;
                if (!visible)
                {
                    continue;
                }
                float distance = count == 1 ? total * 0.5f : total * index / (count - 1);
                SamplePolyline(points, distance, out float x, out float y);
                Widget segment = _routeSegments[index];
                Widget halo = _routeHalos[index];
                segment.PositionXOffset = x - (segment.SuggestedWidth * 0.5f);
                segment.PositionYOffset = y - (segment.SuggestedHeight * 0.5f);
                halo.PositionXOffset = x - (halo.SuggestedWidth * 0.5f);
                halo.PositionYOffset = y - (halo.SuggestedHeight * 0.5f);
                segment.Color = color;
            }
            _routeModePlate.IsVisible = false;
        }

        private void HideRoutePreview()
        {
            foreach (Widget widget in _routeSegments)
            {
                widget.IsVisible = false;
            }
            foreach (Widget widget in _routeHalos)
            {
                widget.IsVisible = false;
            }
            if (_routeModePlate != null)
            {
                _routeModePlate.IsVisible = false;
            }
        }

        private static void SamplePolyline(
            IReadOnlyList<Tuple<float, float>> points,
            float distance,
            out float x,
            out float y)
        {
            for (int index = 1; index < points.Count; index++)
            {
                float dx = points[index].Item1 - points[index - 1].Item1;
                float dy = points[index].Item2 - points[index - 1].Item2;
                float segment = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (distance <= segment)
                {
                    float amount = segment <= 0.001f ? 0f : distance / segment;
                    x = points[index - 1].Item1 + (dx * amount);
                    y = points[index - 1].Item2 + (dy * amount);
                    return;
                }
                distance -= segment;
            }
            x = points[points.Count - 1].Item1;
            y = points[points.Count - 1].Item2;
        }

        private Widget CreateRouteDot(string id, float size, Color color)
        {
            return new Widget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = size,
                SuggestedHeight = size,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteCircle"),
                Color = color,
                DoNotAcceptEvents = true,
                IsVisible = false
            };
        }

        private static bool IsRussianGame => string.Equals(
            TaleWorlds.MountAndBlade.BannerlordConfig.Language,
            "Русский",
            StringComparison.Ordinal);

        private static bool IsTown(DikozemieLobbyMapNode node)
        {
            return string.Equals(node?.Kind, "town", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCastle(DikozemieLobbyMapNode node)
        {
            return string.Equals(node?.Kind, "castle", StringComparison.OrdinalIgnoreCase);
        }

        internal static string DisplayNodeName(DikozemieLobbyMapNode node)
        {
            if (node == null)
            {
                return "Неизвестно";
            }
            string name = IsRussianGame && !string.IsNullOrWhiteSpace(node.NameRu)
                ? node.NameRu
                : node.Name;
            return string.IsNullOrWhiteSpace(name) ? node.Id : name;
        }

        private static string GetKindName(DikozemieLobbyMapNode node)
        {
            if (IsTown(node))
            {
                return "ГОРОД";
            }
            if (IsCastle(node))
            {
                return "ЗАМОК";
            }
            return "ДЕРЕВНЯ";
        }

        private static string GetCultureName(string culture)
        {
            switch ((culture ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "aserai": return "АСЕРАИ";
                case "battania": return "БАТТАНИЯ";
                case "empire": return "ИМПЕРИЯ";
                case "khuzait": return "ХУЗАИТЫ";
                case "nord":
                case "nords": return "НОРДЫ";
                case "sturgia": return "СТУРГИЯ";
                case "vlandia": return "ВЛАНДИЯ";
                default: return "НЕЙТРАЛЬНЫЕ ЗЕМЛИ";
            }
        }

        private static Color GetCultureColor(string culture)
        {
            switch ((culture ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "aserai": return new Color(0.91f, 0.66f, 0.18f, 1f);
                case "battania": return new Color(0.25f, 0.67f, 0.31f, 1f);
                case "empire": return new Color(0.62f, 0.35f, 0.78f, 1f);
                case "khuzait": return new Color(0.24f, 0.70f, 0.68f, 1f);
                case "nord":
                case "nords": return new Color(0.48f, 0.74f, 0.91f, 1f);
                case "sturgia": return new Color(0.35f, 0.53f, 0.82f, 1f);
                case "vlandia": return new Color(0.82f, 0.24f, 0.20f, 1f);
                default: return new Color(0.80f, 0.74f, 0.61f, 1f);
            }
        }

        private Widget CreateSolid(string id, Color color)
        {
            return new Widget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteSquare_9"),
                Color = color,
                DoNotAcceptEvents = true
            };
        }

        private TextWidget CreateText(string id, string text, int size, Color color)
        {
            var widget = new TextWidget(Context)
            {
                Id = id,
                Text = text,
                DoNotAcceptEvents = true
            };
            widget.Brush.Font = Context.FontFactory.GetMappedFontForLocalization("Galahad");
            widget.Brush.FontSize = size;
            widget.Brush.FontColor = color;
            widget.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Center;
            widget.Brush.TextVerticalAlignment = TextVerticalAlignment.Center;
            return widget;
        }
    }

    internal sealed class DikozemieLobbyMapVM : ViewModel
    {
    }
}
