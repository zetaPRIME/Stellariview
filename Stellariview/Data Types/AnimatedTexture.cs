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

using Path = Fluent.IO.Path;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Stellariview
{
	public class AnimatedTexture
	{
		public struct AnimFrame
		{
			public Texture2D texture;
			public float duration;

			public AnimFrame(Texture2D texture, float duration = 0.1f) { this.texture = texture; this.duration = duration; }
		}

		public List<AnimFrame> frames = new List<AnimFrame>();
		public int curFrameId = 0;
		public float frameStartTime = -1;

		public AnimatedTexture(Image img)
		{
			Load(img);
		}

		void Load(Image img)
		{
			FrameDimension dimension = new FrameDimension(img.FrameDimensionsList[0]);
			int frameCount = img.GetFrameCount(dimension);

			PropertyItem frameMeta = img.PropertyItems[0];

			int[] frameDuration = new int[frameMeta.Len / 4];

			int count = 0;
			for (int i = 0; i < frameMeta.Len; i += 4)
			{
				frameDuration[count++] = ((((int)frameMeta.Value[i + 1]) << 8) + frameMeta.Value[i]) * 10;
			}

			int defaultDelay = 10;
			if (frameDuration.Length > 0) defaultDelay = frameDuration[0];

			// actually set up textures
			Texture2D res = null;
			for (int i = 0; i < frameCount; i++)
			{
				int duration = defaultDelay;
				if (i < frameDuration.Length) duration = frameDuration[i];

				img.SelectActiveFrame(dimension, i);
				Bitmap bmp = new Bitmap(img);
				using (MemoryStream ms = new MemoryStream())
				{
					bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
					res = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, ms);
				}

				frames.Add(new AnimFrame(res, (float)duration / 1000f));
			}
		}

		public void Draw(SpriteBatch sb, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effect, float depth)
		{
			Draw(sb, position, sourceRectangle, color, rotation, origin, Vector2.One * scale, effect, depth);
		}
		public void Draw(SpriteBatch sb, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effect, float depth)
		{
			float curTime = Core.frameTimeTotal;
			if (frameStartTime == -1) frameStartTime = curTime;

			while (curTime > frameStartTime + frames[curFrameId].duration)
			{
				frameStartTime += frames[curFrameId].duration;
				curFrameId = (curFrameId + 1) % frames.Count;
			}

			sb.Draw(frames[curFrameId].texture, position, sourceRectangle, color, rotation, origin, scale, effect, depth);
		}
	}
}
