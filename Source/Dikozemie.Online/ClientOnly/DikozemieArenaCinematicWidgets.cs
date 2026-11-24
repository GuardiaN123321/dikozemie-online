using System;
using System.IO;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.ModuleManager;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    public abstract class DikozemieArenaFileTextureProvider : TextureProvider
    {
        private TaleWorlds.Engine.Texture _engineTexture;
        private TaleWorlds.TwoDimension.Texture _texture;
        private bool _loadAttempted;

        protected abstract string FileName { get; }

        protected abstract string TextureName { get; }

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
            string path = Path.Combine(
                ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                "GUI",
                "Textures",
                "Arena",
                FileName);
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine(
                        "DikozemieOnline: UI texture is missing: " + path);
                    return;
                }

                _engineTexture = TaleWorlds.Engine.Texture.CreateFromMemory(
                    File.ReadAllBytes(path));
                if (_engineTexture != null)
                {
                    _engineTexture.Name = TextureName;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: UI texture failed: " + error);
                _engineTexture = null;
            }
        }
    }

    public sealed class DikozemieArenaCinematicTextureProvider :
        DikozemieArenaFileTextureProvider
    {
        protected override string FileName =>
            "death-reward-cinematic.png";

        protected override string TextureName =>
            "dikozemie_arena_death_reward_cinematic";
    }

    public sealed class DikozemieArenaMenuTextureProvider :
        DikozemieArenaFileTextureProvider
    {
        protected override string FileName => "arena-menu-card.png";

        protected override string TextureName => "dikozemie_arena_menu_card";
    }

    public sealed class DikozemieNavalMenuTextureProvider :
        DikozemieArenaFileTextureProvider
    {
        protected override string FileName => "naval-menu-card.png";

        protected override string TextureName => "dikozemie_naval_menu_card";
    }

    /// <summary>
    /// Short impact motion for each staged reward reveal. A new token restarts
    /// the drop-and-fade animation without allocating another Gauntlet layer.
    /// </summary>
    public sealed class DikozemieArenaCinematicPulseWidget : Widget
    {
        private const float Duration = 0.28f;
        private int _triggerToken;
        private float _elapsed = Duration;

        public DikozemieArenaCinematicPulseWidget(UIContext context)
            : base(context)
        {
            DoNotAcceptEvents = true;
        }

        [Editor(false)]
        public int TriggerToken
        {
            get => _triggerToken;
            set
            {
                if (_triggerToken == value)
                {
                    return;
                }
                _triggerToken = value;
                _elapsed = 0f;
                OnPropertyChanged(value, nameof(TriggerToken));
            }
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            _elapsed = Math.Min(Duration, _elapsed + Math.Max(0f, dt));
            float progress = _elapsed / Duration;
            float easeOut = 1f - (1f - progress) * (1f - progress);
            AlphaFactor = Math.Min(1f, progress * 1.8f);
            PositionYOffset = -34f * (1f - easeOut);
        }
    }
}
