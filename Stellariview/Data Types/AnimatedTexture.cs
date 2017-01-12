using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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

using LibAPNG;
using LibAPNG.XNAHelper;

using Path = Fluent.IO.Path;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Stellariview {
    public struct AnimFrame {
        public Texture2D texture;
        public float duration;

        public AnimFrame(Texture2D texture, float duration = 0.1f) { this.texture = texture; this.duration = duration; }
    }

    public class AnimatedTexture {
        public List<AnimFrame> frames = new List<AnimFrame>();
        public int curFrameId = 0;
        public float frameTime = 0;
        int frameDrawCycle = -1;

        bool loop = true;

        public AnimPreparer prep = null;

        public AnimatedTexture(Path sourcePath) {
            Load(sourcePath);
        }

        void Load(Path sourcePath) {
            if (sourcePath.Extension == ".gif") LoadGif(sourcePath);
            else if (sourcePath.Extension == ".png") LoadApng(sourcePath);
        }

        void LoadGif(Path sourcePath) {
            sourcePath.Open((FileStream fs) => {
                Image img = Image.FromStream(fs);

                FrameDimension dimension = new FrameDimension(img.FrameDimensionsList[0]);
                int frameCount = img.GetFrameCount(dimension);

                PropertyItem frameMeta = img.PropertyItems[0];

                int[] frameDuration = new int[frameMeta.Len / 4];

                int count = 0;
                for (int i = 0; i < frameMeta.Len; i += 4) {
                    frameDuration[count++] = ((((int)frameMeta.Value[i + 1]) << 8) + frameMeta.Value[i]) * 10;
                }

                int defaultDelay = 10;
                if (frameDuration.Length > 0) defaultDelay = frameDuration[0];

                // actually set up textures
                Texture2D res = null;
                for (int i = 0; i < frameCount; i++) {
                    int duration = defaultDelay;
                    if (i < frameDuration.Length) duration = frameDuration[i];

                    img.SelectActiveFrame(dimension, i);
                    Bitmap bmp = new Bitmap(img);
                    using (MemoryStream ms = new MemoryStream()) {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        res = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, ms);
                    }

                    frames.Add(new AnimFrame(res, (float)duration / 1000f));
                }

                // finally, add preparer
                prep = new BasicAnimPreparer();
            });
        }

        void LoadApng(Path sourcePath) {
            sourcePath.Open((FileStream fs) => {
                using (MemoryStream ms = new MemoryStream()) // read in first, don't repeat disk I/O
                {
                    fs.CopyTo(ms);
                    ms.Position = 0;
                    byte[] bytes = ms.ReadBytes((int)ms.Length);
                    APNG apng = null;
                    try { apng = new APNG(bytes); }
                    catch (Exception e) { } // gulp apng load error and load as simple png

                    if (apng == null || apng.IsSimplePNG) frames.Add(new AnimFrame(ImageHelper.LoadFromStream((ms))));
                    else {
                        loop = apng.acTLChunk.NumPlays < 1;
                        //Texture2D baseTex = ImageHelper.LoadFromApngFrame(apng.DefaultImage);
                        Texture2D baseTex = ImageHelper.LoadFromApngFrame(apng.Frames[0]);
                        foreach (Frame f in apng.Frames) {
                            float duration = 0.1f;
                            if (f.fcTLChunk != null) duration = (float)f.fcTLChunk.DelayNum / (float)f.fcTLChunk.DelayDen;
                            //Texture2D tex = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, new MemoryStream(f.GetStream().ToArray()));
                            frames.Add(new AnimFrame(baseTex, duration));
                        }
                    }

                    prep = new APNGPreparer(apng);
                }
            });
        }

        public void Prepare(ImageContainer tex) {
            if (prep != null) prep.Prepare(tex, this);

            if (tex.state == ImageContainer.TextureState.Loaded) prep = null;
        }

        public void Draw(SpriteBatch sb, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, float depth) {
            Draw(sb, position, sourceRectangle, color, rotation, origin, Vector2.One * scale, effect, depth);
        }
        public void Draw(SpriteBatch sb, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effect, float depth) {
            if (frameDrawCycle != Core.drawCycleId) frameTime += Core.deltaTimeDraw;
            frameDrawCycle = Core.drawCycleId;

            while (frameTime > frames[curFrameId].duration) {
                if (!loop && curFrameId == frames.Count - 1) {
                    frameTime = 0;
                    break;
                }
                frameTime -= frames[curFrameId].duration;
                curFrameId = (curFrameId + 1) % frames.Count;
            }

            sb.Draw(frames[curFrameId].texture, position, sourceRectangle, color, rotation, origin, scale, effect, depth);
        }
    }
}
