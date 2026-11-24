using System;
using System.IO;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.ModuleManager;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    public abstract class DikozemieHeroCultureTextureProvider : TextureProvider
    {
        private readonly string _cultureId;
        private TaleWorlds.Engine.Texture _engineTexture;
        private TaleWorlds.TwoDimension.Texture _texture;
        private bool _loadAttempted;

        protected DikozemieHeroCultureTextureProvider(string cultureId)
        {
            _cultureId = cultureId;
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
                "HeroCulture",
                _cultureId + ".png");
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine(
                        "DikozemieOnline: hero culture backdrop is missing: " + path);
                    return;
                }

                _engineTexture = TaleWorlds.Engine.Texture.CreateFromMemory(
                    File.ReadAllBytes(path));
                if (_engineTexture != null)
                {
                    _engineTexture.Name = "dikozemie_hero_culture_" + _cultureId;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: hero culture backdrop failed for " +
                    _cultureId + ": " + error);
                _engineTexture = null;
            }
        }
    }

    public sealed class DikozemieHeroCultureVlandiaTextureProvider
        : DikozemieHeroCultureTextureProvider
    {
        public DikozemieHeroCultureVlandiaTextureProvider() : base("vlandia") { }
    }

    public sealed class DikozemieHeroCultureSturgiaTextureProvider
        : DikozemieHeroCultureTextureProvider
    {
        public DikozemieHeroCultureSturgiaTextureProvider() : base("sturgia") { }
    }

    public sealed class DikozemieHeroCultureEmpireTextureProvider
        : DikozemieHeroCultureTextureProvider
    {
        public DikozemieHeroCultureEmpireTextureProvider() : base("empire") { }
    }

    public sealed class DikozemieHeroCultureAseraiTextureProvider
        : DikozemieHeroCultureTextureProvider
    {
        public DikozemieHeroCultureAseraiTextureProvider() : base("aserai") { }
    }

    public sealed class DikozemieHeroCultureKhuzaitTextureProvider
        : DikozemieHeroCultureTextureProvider
    {
        public DikozemieHeroCultureKhuzaitTextureProvider() : base("khuzait") { }
    }

    public sealed class DikozemieHeroCultureBattaniaTextureProvider
        : DikozemieHeroCultureTextureProvider
    {
        public DikozemieHeroCultureBattaniaTextureProvider() : base("battania") { }
    }

    public sealed class DikozemieHeroCultureNordsTextureProvider
        : DikozemieHeroCultureTextureProvider
    {
        public DikozemieHeroCultureNordsTextureProvider() : base("nords") { }
    }

    public sealed class DikozemieHeroCultureBackdropWidget : Widget
    {
        // Vanilla CharacterCreation culture artwork is composed from four
        // transparent 938x840 layers placed inside a 1300x700 host. Preserve
        // those native dimensions for the main painting. List thumbnails use
        // the same image with a deliberate vertical crop, just like vanilla.
        private const float ImageAspectRatio = 952f / 876f;
        private const float NativeImageWidth = 938f;
        private const float NativeImageHeight = 840f;
        private TextureWidget _image;
        private string _currentCultureId = string.Empty;
        private bool _useThumbnail;

        public DikozemieHeroCultureBackdropWidget(UIContext context)
            : base(context)
        {
            ClipContents = true;
            DoNotAcceptEvents = true;
        }

        [Editor(false)]
        public string CurrentCultureId
        {
            get => _currentCultureId;
            set
            {
                string safe = value ?? string.Empty;
                if (string.Equals(_currentCultureId, safe, StringComparison.Ordinal))
                {
                    return;
                }

                _currentCultureId = safe;
                OnPropertyChanged(value, nameof(CurrentCultureId));
                RefreshTextureProvider();
            }
        }

        [Editor(false)]
        public bool UseThumbnail
        {
            get => _useThumbnail;
            set
            {
                if (_useThumbnail == value)
                {
                    return;
                }
                _useThumbnail = value;
                OnPropertyChanged(value, nameof(UseThumbnail));
            }
        }

        protected override void OnConnectedToRoot()
        {
            base.OnConnectedToRoot();
            EnsureImage();
            RefreshTextureProvider();
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            EnsureImage();
            if (_image == null || Size.X <= 0f || Size.Y <= 0f)
            {
                return;
            }

            if (!UseThumbnail)
            {
                _image.SuggestedWidth = NativeImageWidth;
                _image.SuggestedHeight = NativeImageHeight;
                return;
            }

            float imageWidth = Size.X;
            float imageHeight = imageWidth / ImageAspectRatio;
            if (imageHeight < Size.Y)
            {
                imageHeight = Size.Y;
                imageWidth = imageHeight * ImageAspectRatio;
            }

            _image.SuggestedWidth = imageWidth;
            _image.SuggestedHeight = imageHeight;
        }

        private void EnsureImage()
        {
            if (_image != null || Context == null)
            {
                return;
            }

            _image = new TextureWidget(Context)
            {
                Id = "CultureBackdropImage",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                DoNotAcceptEvents = true,
                // never the engine's ResourceTextureProvider (see DikozemieLobbyMapView tiles)
                TextureProviderName = string.Empty
            };
            AddChild(_image);
        }

        private void RefreshTextureProvider()
        {
            if (_image == null)
            {
                return;
            }

            string provider = GetProviderName(_currentCultureId);
            _image.IsVisible = !string.IsNullOrEmpty(provider);
            if (!string.IsNullOrEmpty(provider))
            {
                // TextureWidget keeps the already-created provider even when
                // TextureProviderName changes. Clear it first so selecting a
                // new culture instantiates the matching vanilla backdrop.
                if (!string.Equals(
                        _image.TextureProviderName,
                        provider,
                        StringComparison.Ordinal))
                {
                    _image.OnClearTextureProvider();
                }
                _image.TextureProviderName = provider;
            }
        }

        private static string GetProviderName(string cultureId)
        {
            switch (cultureId)
            {
                case "vlandia": return "DikozemieHeroCultureVlandiaTextureProvider";
                case "sturgia": return "DikozemieHeroCultureSturgiaTextureProvider";
                case "empire": return "DikozemieHeroCultureEmpireTextureProvider";
                case "aserai": return "DikozemieHeroCultureAseraiTextureProvider";
                case "khuzait": return "DikozemieHeroCultureKhuzaitTextureProvider";
                case "battania": return "DikozemieHeroCultureBattaniaTextureProvider";
                case "nords": return "DikozemieHeroCultureNordsTextureProvider";
                default: return string.Empty;
            }
        }
    }
}
