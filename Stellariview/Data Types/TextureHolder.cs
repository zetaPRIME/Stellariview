using System;
using System.Collections.Generic;
using System.Drawing;
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

namespace Stellariview
{
	public class TextureHolder
	{
		const bool DEBUG = true;
		DateTime loadStartTime;

		double loadStartGameTime = 0;

		public enum TextureState { Unloaded, Loading, Loaded }

		public TextureState state = TextureState.Unloaded;

		public Texture2D texture;
		public Path sourcePath;

		public TextureHolder(Path path, bool loadImmediate)
		{
			sourcePath = path;
			if (loadImmediate) Load();
		}

		#region Loading stuffs
		public void LoadImage(Path path)
		{
			sourcePath = path;
			Load();
		}

		public void Load()
		{
			if (state == TextureState.Loaded) return;
			if (state != TextureState.Loading && Core.frameTime != null) loadStartGameTime = Core.frameTime.TotalGameTime.TotalSeconds;
			state = TextureState.Loading;

			if (loadQueue.Contains(this)) loadQueue.Remove(this);
			loadQueue.Insert(0, this);
		}

		public void Unload() {
			texture = null;
			state = TextureState.Unloaded;

			if (loadQueue.Contains(this)) loadQueue.Remove(this);
			allLoaded.Remove(this);
		}

		void LoadFromThread()
		{
			if (DEBUG) loadStartTime = DateTime.Now;
			Texture2D res = null;
			sourcePath.Open((FileStream fs) =>
			{
				try
				{
					res = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, fs);
					res = ConvertToPreMultipliedAlphaGPU(res);
				}
				catch (Exception e)
				{
					if (e.Message.Contains("indexed"))
					{
						Image img = Image.FromStream(fs);
						Bitmap bmp = new Bitmap(img);
						using (MemoryStream ms = new MemoryStream())
						{
							bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
							lock (Core.gfxLock) res = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, ms);
						}
					}
				}
			});
			if (res != null && state == TextureState.Loading)
			{
				texture = res;
				if (DEBUG) Console.WriteLine("Loading of " + sourcePath.FileName + " took " + (DateTime.Now - loadStartTime).TotalSeconds + "s; dimensions " + res.Width + "x" + res.Height);
				state = TextureState.Loaded;

				allLoaded.Add(this);
				loadQueue.Remove(this);
			}
			else Unload();
		}
		#endregion

		public Vector2 GetSize(Vector2 sizeConstraints)
		{
			if (state == TextureState.Loaded)
			{
				Vector2 imgSize = new Vector2(texture.Width, texture.Height);

				float scale = Math.Min(sizeConstraints.X / imgSize.X, sizeConstraints.Y / imgSize.Y);
				scale = Math.Min(scale, 1f);

				return imgSize * scale;
			}

			return Vector2.One * 80;
		}

		public void Draw(SpriteBatch sb, Vector2 position, Vector2 sizeConstraints, Color? color = null)
		{
			Color drawColor = Color.White;
			if (color != null) drawColor = color.Value;

			if (state == TextureState.Loaded)
			{
				Vector2 imgSize = new Vector2(texture.Width, texture.Height);

				float scale = Math.Min(sizeConstraints.X / imgSize.X, sizeConstraints.Y / imgSize.Y);
				scale = Math.Min(scale, 1f);

				sb.Draw(texture, position, null, drawColor, 0f, imgSize / 2f, scale, SpriteEffects.None, 0f);
			}

			else if (state == TextureState.Loading)
			{
				double time = Core.frameTime.TotalGameTime.TotalSeconds - loadStartGameTime;
				float rotation = (float)(time * Math.PI * 1.0);
				//float step = (float)(Math.PI * 0.05);

				/*sb.Draw(Core.txPixel, position, null, new Color(drawColor.ToVector4() * 0.25f), rotation - step * 2, Vector2.One * 0.5f, new Vector2(2f, 80f), SpriteEffects.None, 0f);
				sb.Draw(Core.txPixel, position, null, new Color(drawColor.ToVector4() * 0.5f), rotation - step, Vector2.One * 0.5f, new Vector2(2f, 80f), SpriteEffects.None, 0f);
				sb.Draw(Core.txPixel, position, null, drawColor, rotation, Vector2.One * 0.5f, new Vector2(2f, 80f), SpriteEffects.None, 0f);*/

				const int numDots = 8;
				const float rotStep = (float)(Math.PI * 2 / numDots);

				Vector4 drawColorVec = drawColor.ToVector4();

				for (int i = 0; i < numDots; i++)
				{
					sb.Draw(Core.txPixel, position + new Vector2((float)Math.Cos(rotation - rotStep * i), (float)Math.Sin(rotation - rotStep * i)) * 20f, null, new Color(drawColorVec * (1f - i * (1f/8f))), (float)(Math.PI / 4), Vector2.One * 0.5f, new Vector2(5f, 5f), SpriteEffects.None, 0f);
				}

			}
		}

		Texture2D ConvertToPreMultipliedAlphaGPU(Texture2D texture)
		{
			// code borrowed from http://jakepoz.com/jake_poznanski__speeding_up_xna.html

			GraphicsDevice GraphicsDevice = Core.spriteBatch.GraphicsDevice;
			
			//Setup a render target to hold our final texture which will have premulitplied alpha values
			RenderTarget2D result = new RenderTarget2D(GraphicsDevice, texture.Width, texture.Height);

			GraphicsDevice.SetRenderTarget(result);
			GraphicsDevice.Clear(Color.Black);

			//Multiply each color by the source alpha, and write in just the color values into the final texture
			BlendState blendColor = new BlendState();
			blendColor.ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue;

			blendColor.AlphaDestinationBlend = Blend.Zero;
			blendColor.ColorDestinationBlend = Blend.Zero;

			blendColor.AlphaSourceBlend = Blend.SourceAlpha;
			blendColor.ColorSourceBlend = Blend.SourceAlpha;

			SpriteBatch spriteBatch = new SpriteBatch(GraphicsDevice);
			spriteBatch.Begin(SpriteSortMode.Immediate, blendColor);
			spriteBatch.Draw(texture, texture.Bounds, Color.White);
			spriteBatch.End();

			//Now copy over the alpha values from the PNG source texture to the final one, without multiplying them
			BlendState blendAlpha = new BlendState();
			blendAlpha.ColorWriteChannels = ColorWriteChannels.Alpha;

			blendAlpha.AlphaDestinationBlend = Blend.Zero;
			blendAlpha.ColorDestinationBlend = Blend.Zero;

			blendAlpha.AlphaSourceBlend = Blend.One;
			blendAlpha.ColorSourceBlend = Blend.One;

			spriteBatch.Begin(SpriteSortMode.Immediate, blendAlpha);
			spriteBatch.Draw(texture, texture.Bounds, Color.White);
			spriteBatch.End();

			//Release the GPU back to drawing to the screen
			GraphicsDevice.SetRenderTarget(null);

			return result as Texture2D;
		}

		Texture2D ConvertToPreMultipliedAlpha(Texture2D texture)
		{
			Color[] data = new Color[texture.Width * texture.Height];
			texture.GetData<Color>(data, 0, data.Length);
			for (int i = 0; i < data.Length; i++)
			{
				byte A = data[i].A;
				data[i] = data[i] * (data[i].A / 255f);
				data[i].A = A;
			}
			texture.SetData<Color>(data, 0, data.Length);
			return texture;
		}

		#region Static things
		static Thread loadThread = null;

		public static List<TextureHolder> loadQueue = new List<TextureHolder>();
		public static List<TextureHolder> allLoaded = new List<TextureHolder>();
		const int MAX_LOADED = 16;

		public static void StartLoadThread()
		{
			loadThread = new Thread(LoadThread);
			loadThread.IsBackground = true;
			loadThread.Start();
		}

		public static void LoadThread()
		{
			while (true) {
				if (loadQueue.Count == 0) { Thread.Sleep(0); continue; }

				loadQueue[0].LoadFromThread();
				CleanUp();
			}
		}

		public static void CleanUp()
		{
			while (allLoaded.Count > MAX_LOADED) allLoaded[0].Unload();
		}
		#endregion
	}
}
