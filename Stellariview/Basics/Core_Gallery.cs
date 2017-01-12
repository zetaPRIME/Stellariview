using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading;

using Fluent.IO;
using Ionic.Zip;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

using Path = Fluent.IO.Path;

namespace Stellariview
{
    //

    public partial class Core : Game
    {
        string[] supportedTypes = new[] { ".png", ".jpg", ".gif" };

        public static Path startingPath;
        Path directory;

        List<ImageContainer> entriesOriginal = new List<ImageContainer>();
        List<ImageContainer> entriesCurrent;
        int currentEntryId = 0;
        int lastFrameEntryId = 0;
        ImageContainer CurrentEntry
        {
            get { return entriesCurrent[currentEntryId]; }
            set { currentEntryId = entriesCurrent.IndexOf(value); }
        }
        ImageContainer NextEntry { get { return entriesCurrent[WrapIndex(currentEntryId + 1)]; } }
        ImageContainer PrevEntry { get { return entriesCurrent[WrapIndex(currentEntryId - 1)]; } }
        ImageContainer NextEntry2 { get { return entriesCurrent[WrapIndex(currentEntryId + 2)]; } }
        ImageContainer PrevEntry2 { get { return entriesCurrent[WrapIndex(currentEntryId - 2)]; } }

        RenderTarget2D paneView;
        ImageContainer paneContents;
        int panePosition;
        float paneScroll;

        int fadeLevel = 1;
        float[] fadeLevels = { 1f, 0.75f, 0.5f, 0.25f };

        float switchScrollPos = 0f;
        float switchScrollScale = 0f;
        bool enableUIAnimations = true;
        bool enableBackground = true;

        bool dirty = true;
        public static bool redraw = true;

        public static Texture2D txBG, txCircle;

        void AppInit()
        {
            // build list
            if (startingPath.IsDirectory) directory = startingPath;
            else directory = startingPath.Up();

            List<String> names = new List<string>();
            foreach (Path file in directory.Files(p => supportedTypes.Contains(p.Extension), false)) names.Add(file.FileName);
            NumericComparer nc = new NumericComparer();
            names.Sort(nc); // make sure proper order

            foreach (string name in names) entriesOriginal.Add(new ImageContainer(directory.Combine(name), false));
            entriesCurrent = entriesOriginal;

            CurrentEntry = entriesCurrent.Find(p => (p.sourcePath == startingPath));
            if (currentEntryId < 0) currentEntryId = 0; // no negative numbers >:(

            // build gradient
            txBG = ImageHelper.MakeGradient(1, GraphicsDevice.Adapter.CurrentDisplayMode.Height, new Color(0.1f, 0.1f, 0.125f), new Color(0.2f, 0.2f, 0.25f));
            // and circle
            txCircle = ImageHelper.MakeCircle(512);
            // fuzz circle
            for (int i = 0; i < 8; i++) txCircle = ImageHelper.Fuzz(txCircle, 16 + i);

            // initialize pane
            paneView = new RenderTarget2D(spriteBatch.GraphicsDevice, Window.ClientBounds.Width / 2, Window.ClientBounds.Height);

            ImageContainer.StartLoadThread();

            Window.ClientSizeChanged += (Object s, EventArgs e) => { redraw = true; }; // force redraw on resize
        }

        bool DoRedraw { get { redraw = true; return true; } } // silly hack because I can :D
        void AppUpdate(GameTime gameTime)
        {
            if (entriesCurrent.Count == 0)
            {
                Window.Title = "Stellariview - No images present!";

                if (Input.KeyPressed(Keys.Escape)) Exit();

                return;
            }

            if (lastFrameEntryId != currentEntryId)
            {
                //entriesCurrent[lastFrameEntryId].Unload();
            }
            lastFrameEntryId = currentEntryId;

            #region Processing key input
            bool alt = (Input.KeyHeld(Keys.LeftAlt) || Input.KeyHeld(Keys.RightAlt));
            bool ctrl = (Input.KeyHeld(Keys.LeftControl) || Input.KeyHeld(Keys.RightControl));
            bool shift = (Input.KeyHeld(Keys.LeftShift) || Input.KeyHeld(Keys.RightShift));

            if (alt)
            {
                if (Input.KeyPressed(Keys.Enter))
                {
                    //Window.IsBorderless = !Window.IsBorderless;
                    ToggleFullscreen();
                }
            }
            else
            {
                if (Input.KeyPressed(Keys.Escape))
                {
                    if (graphics.IsFullScreen) ToggleFullscreen();
                    else Exit();
                }

                if (Input.KeyPressed(Keys.F11)) ToggleFullscreen();

                if (Input.KeyPressed(Keys.A)) enableUIAnimations = !enableUIAnimations;
                if (Input.KeyPressed(Keys.B) && DoRedraw) enableBackground = !enableBackground;

                if (Input.KeyPressed(Keys.S) && DoRedraw) Shuffle();
                if (Input.KeyPressed(Keys.F) && DoRedraw) fadeLevel = (fadeLevel + 1) % fadeLevels.Length;

                if (Input.KeyPressed(Keys.P) || (ctrl && Input.KeyPressed(Keys.Tab)))
                {
                    ImageContainer cache = paneContents;
                    paneContents = CurrentEntry;
                    if (shift && cache != null) CurrentEntry = cache;
                    dirty = true;

                    if (panePosition == 0) panePosition = -1; // pop up if not up already
                    redraw = true;
                }
                if (Input.KeyPressed(Keys.Tab) && !ctrl)
                {
                    if (paneContents == null) paneContents = CurrentEntry;

                    int tgt = -1;
                    if (shift) tgt = 1;

                    if (panePosition == tgt) panePosition = 0;
                    else
                    {
                        panePosition = tgt;
                        paneContents.Load();
                    }
                }

                if (Input.KeyPressed(Keys.Left) || Input.KeyPressed(Keys.Z))
                {
                    if (!shift || entriesCurrent == entriesOriginal) GoPrev();
                    else
                    {
                        int fid = entriesOriginal.IndexOf(entriesCurrent[currentEntryId]);
                        currentEntryId = entriesCurrent.IndexOf(entriesOriginal[WrapIndex(fid - 1)]);

                        dirty = DoRedraw;
                    }
                }
                if (Input.KeyPressed(Keys.Right) || Input.KeyPressed(Keys.X))
                {
                    if (!shift || entriesCurrent == entriesOriginal) GoNext();
                    else
                    {
                        int fid = entriesOriginal.IndexOf(entriesCurrent[currentEntryId]);
                        currentEntryId = entriesCurrent.IndexOf(entriesOriginal[WrapIndex(fid + 1)]);

                        dirty = DoRedraw;
                    }
                }
            }
            #endregion

            if (dirty)
            {
                ImageContainer.pauseLoad = true;
                entriesCurrent[WrapIndex(currentEntryId - 2)].Load();
                entriesCurrent[WrapIndex(currentEntryId + 2)].Load();
                entriesCurrent[WrapIndex(currentEntryId - 1)].Load();
                entriesCurrent[WrapIndex(currentEntryId + 1)].Load();
                if (paneContents != null && paneScroll != 0f) paneContents.Load();
                CurrentEntry.Load();
                ImageContainer.pauseLoad = false;

                string title = "Stellariview - " + CurrentEntry.sourcePath.FileName;
                if (entriesCurrent != entriesOriginal) title += " (shuffle)";
                Window.Title = title;

                dirty = false;
            }
        }

        bool StateForcesRedraw(ImageContainer ct)
        {
            return ct.animation != null || ct.state == ImageContainer.TextureState.Loading || ct.state == ImageContainer.TextureState.Preparing;
        }

        void AppDraw(GameTime gameTime)
        {
            if (switchScrollScale != 0) redraw = true;
            else if (paneScroll != panePosition) redraw = true;
            else if (entriesCurrent.Count > 0 && (StateForcesRedraw(CurrentEntry) || StateForcesRedraw(PrevEntry) || StateForcesRedraw(NextEntry)
                || StateForcesRedraw(PrevEntry2) || StateForcesRedraw(NextEntry2))) redraw = true;
            if (!redraw) return;
            redraw = false;


            if (entriesCurrent.Count == 0)
            {
                spriteBatch.GraphicsDevice.Clear(new Color(0.25f, 0f, 0f));
                return;
            }

            float curFadeLevel = fadeLevels[fadeLevel];
            Color fadeColor = new Color(new Vector4(curFadeLevel));
            Color fadeColor2 = new Color(new Vector4(curFadeLevel * curFadeLevel));

            // set up pane position
            if (paneContents == null) paneScroll = panePosition = 0;
            else if (!enableUIAnimations) paneScroll = panePosition;
            else
            {
                float scSpeed = deltaTimeDraw * ((9.5f * Math.Abs(paneScroll - panePosition)) + 0.5f);
                if (paneScroll < panePosition)
                {
                    paneScroll += scSpeed;
                    if (paneScroll > panePosition) paneScroll = panePosition;
                }
                else if (paneScroll > panePosition)
                {
                    paneScroll -= scSpeed;
                    if (paneScroll < panePosition) paneScroll = panePosition;
                }
            }

            Vector2 screenSize = new Vector2(Window.ClientBounds.Width, Window.ClientBounds.Height);

            Vector2 paneSize = new Vector2((int)(screenSize.X / 2), screenSize.Y);
            if (paneContents != null) paneSize = new Vector2((int)paneContents.GetSize(paneSize).X, paneSize.Y);

            Vector2 viewSize = screenSize - new Vector2(paneSize.X * Math.Abs(paneScroll), 0);
            Vector2 screenCenter = viewSize * 0.5f + new Vector2(paneSize.X * Math.Max(0, paneScroll), 0);

            const float PER_SECOND = 0.64f;
            const float PER_SECOND_INVERT = 1 - PER_SECOND;
            float proportion = PER_SECOND + (PER_SECOND_INVERT * deltaTimeDraw);
            proportion = Math.Max(0f, Math.Min(proportion, 1f));
            switchScrollScale *= proportion;
            if (!enableUIAnimations) switchScrollScale = 0;

            if (Math.Abs(switchScrollScale) < 0.005f) switchScrollScale = 0;

            Vector2 drawOrigin = screenCenter + new Vector2(switchScrollPos * switchScrollScale, 0);

            Vector2 ces = CurrentEntry.GetSize(viewSize);
            if (ces.X % 2 != viewSize.X % 2) drawOrigin += new Vector2(0.5f, 0f); // enforce clarity on dimensions not matching parity

            #region Pane rendertarget
            if (paneContents != null && paneSize.X > 0 && paneSize.Y > 0)
            {
                if (paneView.Width != (int)paneSize.X || paneView.Height != (int)paneSize.Y)
                {
                    paneView = new RenderTarget2D(spriteBatch.GraphicsDevice, (int)paneSize.X, (int)paneSize.Y);
                }

                spriteBatch.GraphicsDevice.SetRenderTarget(paneView);
                spriteBatch.GraphicsDevice.Clear(Color.Black);

                spriteBatch.Begin();

                if (enableBackground) spriteBatch.Draw(txBG, paneView.Bounds, Color.White); // bg

                Vector2 ppos = paneSize * 0.5f;
                if (paneContents.GetSize(paneSize).Y % 2 != paneSize.Y % 2) ppos += new Vector2(0f, 0.5f); // 1x1 pixel position, vertical

                paneContents.Draw(spriteBatch, paneSize * 0.5f, paneSize);

                spriteBatch.End();

                spriteBatch.GraphicsDevice.SetRenderTarget(null);
            }
            #endregion

            #region Main gallery draw
            spriteBatch.GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();//SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, RasterizerState.CullNone);
                                //spriteBatch.Draw(imgCurrent, Vector2.Zero, Color.White);

            if (enableBackground) spriteBatch.Draw(txBG, new Rectangle(Point.Zero, Window.ClientBounds.Size), Color.White); // bg

            PrevEntry2.Draw(spriteBatch, drawOrigin - new Vector2(CurrentEntry.GetSize(viewSize).X * 0.5f + PrevEntry.GetSize(viewSize).X + PrevEntry2.GetSize(viewSize).X * 0.5f, 0), viewSize, fadeColor2);
            NextEntry2.Draw(spriteBatch, drawOrigin + new Vector2(CurrentEntry.GetSize(viewSize).X * 0.5f + NextEntry.GetSize(viewSize).X + NextEntry2.GetSize(viewSize).X * 0.5f, 0), viewSize, fadeColor2);

            PrevEntry.Draw(spriteBatch, drawOrigin - new Vector2(CurrentEntry.GetSize(viewSize).X * 0.5f + PrevEntry.GetSize(viewSize).X * 0.5f, 0), viewSize, fadeColor);
            NextEntry.Draw(spriteBatch, drawOrigin + new Vector2(CurrentEntry.GetSize(viewSize).X * 0.5f + NextEntry.GetSize(viewSize).X * 0.5f, 0), viewSize, fadeColor);

            Vector2 parity = Vector2.Zero;
            if (ces.Y % 2 != viewSize.Y % 2) parity += new Vector2(0f, 0.5f); // enforce clarity on dimensions not matching parity
            CurrentEntry.Draw(spriteBatch, drawOrigin + parity, viewSize);

            if (paneScroll != 0f)
            {
                float psw = paneSize.X * paneScroll * -1f;
                // right
                if (psw > 0)
                    spriteBatch.Draw(paneView, new Vector2(screenSize.X - psw, screenSize.Y / 2f), null, Color.White, 0f, new Vector2(0, paneSize.Y / 2f), 1f, SpriteEffects.None, 0f);

                // left
                if (psw < 0)
                    spriteBatch.Draw(paneView, new Vector2(-psw, screenSize.Y / 2f), null, Color.White, 0f, new Vector2(paneSize.X, paneSize.Y / 2f), 1f, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            #endregion
        }

        void GoPrev()
        {
            Vector2 screenSize = new Vector2(Window.ClientBounds.Width, Window.ClientBounds.Height);

            float curScrollPos = switchScrollPos * switchScrollScale;
            switchScrollPos = curScrollPos - (CurrentEntry.GetSize(screenSize).X * 0.5f + PrevEntry.GetSize(screenSize).X * 0.5f);
            SwitchScroll(screenSize);

            currentEntryId = WrapIndex(currentEntryId - 1);
            dirty = true;
        }
        void GoNext()
        {
            Vector2 screenSize = new Vector2(Window.ClientBounds.Width, Window.ClientBounds.Height);

            float curScrollPos = switchScrollPos * switchScrollScale;
            switchScrollPos = curScrollPos + (CurrentEntry.GetSize(screenSize).X * 0.5f + NextEntry.GetSize(screenSize).X * 0.5f);
            SwitchScroll(screenSize);

            currentEntryId = WrapIndex(currentEntryId + 1);
            dirty = true;
        }
        void SwitchScroll(Vector2 screenSize)
        {
            switchScrollPos = Math.Max(-screenSize.X, Math.Min(switchScrollPos, screenSize.X)); // clamp to reasonable value so the screen doesn't get left behind
            switchScrollScale = 1f;

            if (switchScrollPos != 0)
            {
                while (Math.Abs(switchScrollPos) < 128)
                {
                    switchScrollPos *= 2f;
                    switchScrollScale /= 2f;
                }
            }
        }

        void WrapIndex() { currentEntryId = WrapIndex(currentEntryId); }
        int WrapIndex(int index)
        {
            while (index < 0) index += entriesCurrent.Count;
            while (index >= entriesCurrent.Count) index -= entriesCurrent.Count;
            return index;
        }

        void Shuffle()
        {
            ImageContainer current = CurrentEntry;

            if (entriesCurrent != entriesOriginal) entriesCurrent = entriesOriginal;
            else
            {
                Random rand = new Random();

                entriesCurrent = new List<ImageContainer>();
                foreach (ImageContainer entry in entriesOriginal)
                {
                    entriesCurrent.Insert(rand.Next(entriesCurrent.Count), entry);
                }
            }

            CurrentEntry = current;

            dirty = true;
        }

        /*TextureHolder LoadImage(string name)
		{
			return new TextureHolder(directory.Combine(name));
		}*/
    }
}
